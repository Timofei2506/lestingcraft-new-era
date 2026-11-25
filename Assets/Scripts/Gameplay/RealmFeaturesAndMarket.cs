using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Gameplay
{
    public readonly struct ResourceTradeOffer
    {
        public readonly string Code, Title, Subtitle;
        public readonly ushort ItemId;
        public readonly int Count;
        public readonly int CoinValue;
        public readonly bool IsSellFromPlayer;

        public ResourceTradeOffer(string code, string title, string subtitle, ushort itemId, int count, int coinValue, bool isSell)
        {
            Code = code; Title = title; Subtitle = subtitle;
            ItemId = itemId; Count = count; CoinValue = coinValue; IsSellFromPlayer = isSell;
        }
    }

    public readonly struct ArchitectBlueprint
    {
        public readonly string Code, Title, Category, Description;
        public readonly int PriceCoins;
        public readonly int SizeX, SizeY, SizeZ;

        public ArchitectBlueprint(string code, string title, string category, string description, int priceCoins, int sx, int sy, int sz)
        {
            Code = code; Title = title; Category = category; Description = description;
            PriceCoins = priceCoins; SizeX = sx; SizeY = sy; SizeZ = sz;
        }

        public string SizeHint => SizeX + "×" + SizeY + "×" + SizeZ;
    }

    public readonly struct GuildContractDef
    {
        public readonly string Id, Title, Description;
        public readonly int Target;
        public readonly int RewardCoins;
        public readonly ushort BonusItemId;
        public readonly int BonusItemCount;

        public GuildContractDef(string id, string title, string desc, int target, int coins, ushort bonusItem, int bonusCount)
        {
            Id = id; Title = title; Description = desc; Target = target;
            RewardCoins = coins; BonusItemId = bonusItem; BonusItemCount = bonusCount;
        }
    }

    [Serializable]
    public sealed class WaypointEntry
    {
        public string name = "Точка";
        public int x, y, z;
        public int dimension;
        public string kind = "custom"; // home, death, portal, custom
    }

    [Serializable]
    sealed class WaypointSaveList
    {
        public WaypointEntry[] items = Array.Empty<WaypointEntry>();
    }

    /// <summary>
    /// Full in-game & hybrid Marketplace Exchange (Resource Selling, Supply Kits, 8 Instant 3D Blueprints),
    /// Guild Contracts / Quests, Persistent Waypoints, and Live Voxel Minimap Renderer.
    /// </summary>
    public static class RealmFeaturesAndMarket
    {
        const string GuildCoinsKey = "LC.GuildCoins.V1";
        const string BlueprintUnlockPrefix = "LC.BP.Unlocked.";
        const string ActiveBlueprintKey = "LC.BP.Active";
        const string ContractProgPrefix = "LC.Contract.Prog.";
        const string ContractClaimPrefix = "LC.Contract.Claim.";
        const string WaypointsPrefix = "LC.Waypoints.Seed.";

        public static readonly ResourceTradeOffer[] SellOffers =
        {
            new ResourceTradeOffer("sell_wood", "Партия древесины ×12", "Продать брёвна строительной гильдии", BlockRegistry.WoodId, 12, 40, true),
            new ResourceTradeOffer("sell_stone", "Тёсаный камень ×24", "Поставка камня для городских мостовых", BlockRegistry.StoneId, 24, 35, true),
            new ResourceTradeOffer("sell_ore", "Рудный концентрат ×6", "Продажа добытой руды кузнецам", BlockRegistry.OreId, 6, 65, true),
            new ResourceTradeOffer("sell_crystal", "Сияющие кристаллы ×4", "Высокий спрос у небесных алхимиков", BlockRegistry.CrystalId, 4, 95, true),
            new ResourceTradeOffer("sell_magma", "Магма-базальт Ада ×8", "Трофейная порода из Базальтовой Преисподней", BlockRegistry.MagmaBasaltId, 8, 120, true),
            new ResourceTradeOffer("sell_soul", "Кристаллы Преисподней ×3", "Редчайший резонатор из глубин Ада", BlockRegistry.AndromedaCrystalId, 3, 180, true)
        };

        public static readonly ResourceTradeOffer[] BuySupplies =
        {
            new ResourceTradeOffer("buy_lanterns", "Набор фонарей и факелов ×12", "Яркие уличные фонари для города и шахт", BlockRegistry.LanternBlockId, 12, 45, false),
            new ResourceTradeOffer("buy_marble", "Античный мрамор ×32", "Белоснежный полированный мрамор для дворцов", 42, 32, 60, false),
            new ResourceTradeOffer("buy_pickaxe", "Алмазная кирка мастера", "Мгновенно раскалывает базальт, руду и обсидиан", 150, 1, 140, false),
            new ResourceTradeOffer("buy_blade", "Алмазный клинок стража", "Мощное оружие против боссов и тварей Ада", 152, 1, 155, false),
            new ResourceTradeOffer("buy_ritual_saplings", "Набор ритуальных саженцев ×8", "Вырастите 2 дуба без костной муки в 7 блоках и сожгите их для Арки в Ад!", BlockRegistry.OakSaplingId, 8, 55, false),
            new ResourceTradeOffer("buy_eden_flowers", "Букет цветов Эдема ×12", "Расставьте вокруг колодца 2×2×5 с водой и бросьте алмаз для входа в Рай!", BlockRegistry.PoppyBlockId, 12, 50, false),
            new ResourceTradeOffer("buy_rails", "Комплект стальных рельсов ×24", "Скоростная железная дорога между городами", BlockRegistry.SteelRailId, 24, 85, false)
        };

        public static readonly ArchitectBlueprint[] Blueprints =
        {
            new ArchitectBlueprint("cozy_cottage", "Уютный Дом Путника", "Жильё",
                "Тёплый фахверковый коттедж 7×7 с черепичной крышей, кроватью, верстаком и фонарём. Открыт сразу бесплатно!", 0, 7, 7, 7),
            new ArchitectBlueprint("eden_pavilion", "Хрустальная Ротонда Эдема", "Святилище",
                "Белоснежный мраморный павильон с золотыми колоннами, фонарями и цветущим садом.", 90, 7, 8, 7),
            new ArchitectBlueprint("watchtower", "Сторожевая Башня Зари", "Оборона",
                "Каменная дозорная башня 5×11×5 с зубцами, смотровой площадкой и сигнальным огнём.", 75, 5, 11, 5),
            new ArchitectBlueprint("smithy_forge", "Кузница и Плавильня", "Ремесло",
                "Мастерская кузнеца с горном, печью, верстаком, костром и каменным навесом.", 80, 7, 6, 7),
            new ArchitectBlueprint("marble_fountain", "Мраморный Фонтан", "Город",
                "Парадный городской фонтан с живой водой и кристальным маяком в центре.", 65, 7, 7, 7),
            new ArchitectBlueprint("sakura_gazebo", "Беседка Цветущей Сакуры", "Сад",
                "Резная дубовая ротонда под пышным куполом розовой сакуры с тёплыми фонарями.", 70, 7, 8, 7),
            new ArchitectBlueprint("canyon_bridge", "Арочный Мост (13 м)", "Инженерия",
                "Надёжный каменный мост с перилами и фонарями через реку, каньон или лаву.", 60, 5, 5, 13),
            new ArchitectBlueprint("field_bunker", "Экспедиционный Бункер", "Защита",
                "Герметичное укрытие из армированного бетона и бронестекла с прожекторами.", 130, 7, 5, 7)
        };

        public static readonly GuildContractDef[] Contracts =
        {
            new GuildContractDef("sky_explorer", "Покоритель Небесных Островов",
                "Поднимитесь на любой парящий остров (высота Y ≥ 130)", 1, 150, BlockRegistry.CrystalId, 6),
            new GuildContractDef("hell_pioneer", "Шаг в Преисподнюю",
                "Откройте портал и ступите в новое измерение «Ад»", 1, 220, 150, 1),
            new GuildContractDef("master_builder", "Градостроитель Новой Эры",
                "Установите 25 блоков или примените чертёж постройки [B]", 25, 110, 42, 32),
            new GuildContractDef("deep_miner", "Геолог Глубин",
                "Добудьте 20 блоков камня, руды или кристаллов", 20, 95, BlockRegistry.LanternBlockId, 8),
            new GuildContractDef("coop_host", "Хозяин Сетевого Мира",
                "Откройте локальный мир по LAN или создайте свой сервер", 1, 140, BlockRegistry.DreamMonolithId, 12),
            new GuildContractDef("market_trader", "Купец Маркетплейса",
                "Совершите любую сделку на бирже ресурсов или чертежей", 1, 80, BlockRegistry.WoodId, 16),
            new GuildContractDef("ritual_forester", "Хранитель Огненной Арки",
                "Вырастите 2 дерева без костной муки в 7 блоках и сожгите их в Арку Ада", 1, 240, 149, 2),
            new GuildContractDef("eden_explorer", "Вознесение в Рай",
                "Откройте цветочный колодец (2×2×5) алмазом и ступите в мир «Рай»", 1, 260, BlockRegistry.AndromedaCrystalId, 8)
        };

        public static int GuildCoins
        {
            get => PlayerPrefs.GetInt(GuildCoinsKey, 350);
            set { PlayerPrefs.SetInt(GuildCoinsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static bool IsBlueprintUnlocked(string code)
        {
            if (code == "cozy_cottage") return true;
            return PlayerPrefs.GetInt(BlueprintUnlockPrefix + code, 0) == 1;
        }

        public static void UnlockBlueprint(string code)
        {
            PlayerPrefs.SetInt(BlueprintUnlockPrefix + code, 1);
            PlayerPrefs.SetString(ActiveBlueprintKey, code);
            PlayerPrefs.Save();
        }

        public static string ActiveBlueprintCode
        {
            get
            {
                string cur = PlayerPrefs.GetString(ActiveBlueprintKey, "cozy_cottage");
                return IsBlueprintUnlocked(cur) ? cur : "cozy_cottage";
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString(ActiveBlueprintKey, value);
                    PlayerPrefs.Save();
                }
            }
        }

        public static ArchitectBlueprint GetActiveBlueprint()
        {
            string code = ActiveBlueprintCode;
            foreach (var b in Blueprints)
                if (b.Code == code) return b;
            return Blueprints[0];
        }

        public static ArchitectBlueprint CycleNextUnlockedBlueprint()
        {
            string cur = ActiveBlueprintCode;
            int idx = 0;
            for (int i = 0; i < Blueprints.Length; i++)
                if (Blueprints[i].Code == cur) { idx = i; break; }
            for (int step = 1; step <= Blueprints.Length; step++)
            {
                var cand = Blueprints[(idx + step) % Blueprints.Length];
                if (IsBlueprintUnlocked(cand.Code))
                {
                    ActiveBlueprintCode = cand.Code;
                    return cand;
                }
            }
            return Blueprints[0];
        }

        public static int GetContractProgress(string id) => PlayerPrefs.GetInt(ContractProgPrefix + id, 0);
        public static bool IsContractClaimed(string id) => PlayerPrefs.GetInt(ContractClaimPrefix + id, 0) == 1;

        public static void AddContractProgress(string id, int amount = 1)
        {
            if (amount <= 0) return;
            foreach (var c in Contracts)
            {
                if (c.Id != id) continue;
                int cur = GetContractProgress(id);
                if (cur < c.Target)
                {
                    PlayerPrefs.SetInt(ContractProgPrefix + id, Mathf.Min(c.Target, cur + amount));
                    PlayerPrefs.Save();
                }
                return;
            }
        }

        public static bool TryExecuteTrade(ResourceTradeOffer offer, Inventory inv, out string message)
        {
            if (inv == null)
            {
                message = "Откройте мир, чтобы торговать с использованием инвентаря!";
                return false;
            }
            if (offer.IsSellFromPlayer)
            {
                int have = inv.Count(offer.ItemId);
                if (have < offer.Count)
                {
                    message = "Не хватает ресурса в рюкзаке (" + have + " / " + offer.Count + ").";
                    return false;
                }
                if (!inv.Remove(offer.ItemId, offer.Count))
                {
                    message = "Не удалось списать предметы из рюкзака.";
                    return false;
                }
                GuildCoins += offer.CoinValue;
                AddContractProgress("market_trader", 1);
                message = "✓ Продано: " + offer.Title + " → +" + offer.CoinValue + " монет Гильдии!";
                return true;
            }

            if (GuildCoins < offer.CoinValue)
            {
                message = "Недостаточно Монет Гильдии (" + GuildCoins + " / " + offer.CoinValue + ").";
                return false;
            }
            if (!inv.CanAdd(offer.ItemId, offer.Count))
            {
                message = "В рюкзаке нет свободного места для покупки!";
                return false;
            }
            GuildCoins -= offer.CoinValue;
            inv.Add(offer.ItemId, offer.Count);
            AddContractProgress("market_trader", 1);
            message = "✓ Куплено: " + offer.Title + " (добавлено в рюкзак)!";
            return true;
        }

        public static bool TryClaimContract(string id, Inventory inv, out string message)
        {
            foreach (var c in Contracts)
            {
                if (c.Id != id) continue;
                if (IsContractClaimed(id)) { message = "Награда за этот контракт уже получена."; return false; }
                if (GetContractProgress(id) < c.Target) { message = "Контракт ещё не выполнен (" + GetContractProgress(id) + "/" + c.Target + ")."; return false; }
                PlayerPrefs.SetInt(ContractClaimPrefix + id, 1);
                GuildCoins += c.RewardCoins;
                if (inv != null && c.BonusItemId > 0 && c.BonusItemCount > 0)
                    inv.Add(c.BonusItemId, c.BonusItemCount);
                PlayerPrefs.Save();
                message = "★ Контракт выполнен! +" + c.RewardCoins + " монет Гильдии" +
                          (inv != null && c.BonusItemCount > 0 ? " и бонус в инвентарь!" : "!");
                return true;
            }
            message = "Контракт не найден.";
            return false;
        }

        // =====================================================================
        // PERSISTENT WAYPOINTS (HOME, DEATH MARKER, PORTALS & CUSTOM PINS)
        // =====================================================================

        public static List<WaypointEntry> LoadWaypoints(int seed)
        {
            string raw = PlayerPrefs.GetString(WaypointsPrefix + seed, "");
            if (string.IsNullOrEmpty(raw)) return new List<WaypointEntry>();
            try
            {
                var parsed = JsonUtility.FromJson<WaypointSaveList>(raw);
                if (parsed != null && parsed.items != null)
                    return new List<WaypointEntry>(parsed.items);
            }
            catch { }
            return new List<WaypointEntry>();
        }

        public static void SaveWaypoints(int seed, List<WaypointEntry> list)
        {
            while (list.Count > 16) list.RemoveAt(0);
            var wrap = new WaypointSaveList { items = list.ToArray() };
            PlayerPrefs.SetString(WaypointsPrefix + seed, JsonUtility.ToJson(wrap));
            PlayerPrefs.Save();
        }

        public static void ClearWaypoints(int seed)
        {
            PlayerPrefs.DeleteKey(WaypointsPrefix + seed);
            PlayerPrefs.Save();
        }

        public static void SetOrUpdateWaypoint(int seed, string name, Vector3 pos, Dimension dim, string kind = "custom")
        {
            var list = LoadWaypoints(seed);
            int bx = Mathf.RoundToInt(pos.x), by = Mathf.RoundToInt(pos.y), bz = Mathf.RoundToInt(pos.z);
            for (int i = 0; i < list.Count; i++)
            {
                if ((kind != "custom" && list[i].kind == kind) || string.Equals(list[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    list[i].name = name;
                    list[i].x = bx; list[i].y = by; list[i].z = bz;
                    list[i].dimension = (int)dim;
                    list[i].kind = kind;
                    SaveWaypoints(seed, list);
                    return;
                }
            }
            list.Add(new WaypointEntry { name = name, x = bx, y = by, z = bz, dimension = (int)dim, kind = kind });
            SaveWaypoints(seed, list);
        }

        public static bool TryGetWaypoint(int seed, string kindOrName, out WaypointEntry found)
        {
            var list = LoadWaypoints(seed);
            foreach (var w in list)
            {
                if (string.Equals(w.kind, kindOrName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(w.name, kindOrName, StringComparison.OrdinalIgnoreCase))
                {
                    found = w;
                    return true;
                }
            }
            found = null;
            return false;
        }

        // =====================================================================
        // 8 INSTANT 3D ARCHITECT BLUEPRINTS
        // =====================================================================

        public static int ConstructBlueprint(string code, int cx, int baseY, int cz, Action<int, int, int, ushort> place)
        {
            int count = 0;
            Action<int, int, int, ushort> Set = (dx, dy, dz, b) =>
            {
                if (baseY + dy <= 1 || baseY + dy >= 250) return;
                place(cx + dx, baseY + dy, cz + dz, b);
                count++;
            };

            switch (code)
            {
                case "eden_pavilion":
                {
                    for (int dz = -3; dz <= 3; dz++)
                        for (int dx = -3; dx <= 3; dx++)
                            Set(dx, 0, dz, Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1 ? BlockRegistry.CrystalId : (ushort)42);
                    foreach (int sx in new[] { -3, 3 })
                        foreach (int sz in new[] { -3, 3 })
                        {
                            for (int ry = 1; ry <= 5; ry++)
                                Set(sx, ry, sz, ry == 5 ? (ushort)58 : (ushort)42);
                        }
                    for (int dz = -3; dz <= 3; dz++)
                        for (int dx = -3; dx <= 3; dx++)
                            if (Math.Abs(dx) >= 2 || Math.Abs(dz) >= 2)
                                Set(dx, 6, dz, (ushort)42);
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++)
                            Set(dx, 7, dz, dx == 0 && dz == 0 ? BlockRegistry.AndromedaCrystalId : (ushort)30);
                    Set(0, 5, 0, BlockRegistry.LanternBlockId);
                    Set(-2, 1, 0, BlockRegistry.DandelionBlockId);
                    Set(2, 1, 0, BlockRegistry.PoppyBlockId);
                    Set(0, 1, -2, BlockRegistry.CornflowerBlockId);
                    Set(0, 1, 2, BlockRegistry.LavenderFlowerId);
                    break;
                }

                case "watchtower":
                {
                    for (int ry = 0; ry <= 9; ry++)
                        for (int dz = -2; dz <= 2; dz++)
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                bool edge = Math.Abs(dx) == 2 || Math.Abs(dz) == 2;
                                bool corner = Math.Abs(dx) == 2 && Math.Abs(dz) == 2;
                                if (ry == 0 || ry == 4 || ry == 8)
                                    Set(dx, ry, dz, edge ? (ushort)41 : BlockRegistry.PlanksId);
                                else if (ry == 9)
                                {
                                    if (edge && ((dx + dz) & 1) == 0) Set(dx, ry, dz, 41);
                                    else if (dx == 0 && dz == 0) Set(0, ry, 0, BlockRegistry.LanternBlockId);
                                }
                                else if (edge)
                                {
                                    bool door = dz == -2 && dx == 0 && ry <= 2;
                                    bool window = (dx == 0 || dz == 0) && (ry == 2 || ry == 6);
                                    Set(dx, ry, dz, door ? BlockRegistry.AirId : window ? (ushort)44 : corner ? BlockRegistry.CarvedOakId : (ushort)41);
                                }
                                else
                                    Set(dx, ry, dz, BlockRegistry.AirId);
                            }
                    break;
                }

                case "smithy_forge":
                {
                    for (int dz = -3; dz <= 3; dz++)
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            Set(dx, 0, dz, 41);
                            bool post = Math.Abs(dx) == 3 && Math.Abs(dz) == 3;
                            for (int ry = 1; ry <= 3; ry++)
                                Set(dx, ry, dz, post ? BlockRegistry.CarvedOakId : (dz == 3 && Math.Abs(dx) <= 2 && ry <= 2 ? (ushort)41 : BlockRegistry.AirId));
                            Set(dx, 4, dz, BlockRegistry.ShinglesId);
                        }
                    Set(-1, 1, 2, BlockRegistry.FurnaceId);
                    Set(0, 1, 2, BlockRegistry.CampfireId);
                    Set(1, 1, 2, BlockRegistry.WorkbenchId);
                    Set(0, 3, 0, BlockRegistry.LanternBlockId);
                    break;
                }

                case "marble_fountain":
                {
                    for (int dz = -3; dz <= 3; dz++)
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            int m = Math.Max(Math.Abs(dx), Math.Abs(dz));
                            Set(dx, 0, dz, 42);
                            if (m == 3) Set(dx, 1, dz, 42);
                            else if (m >= 1 && m <= 2) Set(dx, 1, dz, BlockRegistry.WaterId);
                        }
                    for (int ry = 1; ry <= 4; ry++) Set(0, ry, 0, 42);
                    Set(0, 5, 0, BlockRegistry.CrystalId);
                    Set(0, 6, 0, BlockRegistry.LanternBlockId);
                    break;
                }

                case "sakura_gazebo":
                {
                    for (int dz = -3; dz <= 3; dz++)
                        for (int dx = -3; dx <= 3; dx++)
                        {
                            Set(dx, 0, dz, BlockRegistry.PlanksId);
                            bool post = Math.Abs(dx) == 3 && Math.Abs(dz) == 3;
                            for (int ry = 1; ry <= 3; ry++)
                                Set(dx, ry, dz, post ? BlockRegistry.CarvedOakId : BlockRegistry.AirId);
                            if (Math.Abs(dx) + Math.Abs(dz) <= 5)
                                Set(dx, 4, dz, BlockRegistry.CherryLeavesId);
                            if (Math.Abs(dx) + Math.Abs(dz) <= 3)
                                Set(dx, 5, dz, BlockRegistry.CherryLeavesId);
                        }
                    Set(0, 3, 0, BlockRegistry.LanternBlockId);
                    break;
                }

                case "canyon_bridge":
                {
                    for (int dz = -6; dz <= 6; dz++)
                    {
                        int arch = 1 - (dz * dz) / 20;
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            Set(dx, arch, dz, Math.Abs(dx) == 2 ? (ushort)41 : BlockRegistry.PlanksId);
                            if (Math.Abs(dx) == 2)
                                Set(dx, arch + 1, dz, dz % 4 == 0 ? BlockRegistry.LanternBlockId : (ushort)41);
                            else
                            {
                                Set(dx, arch + 1, dz, BlockRegistry.AirId);
                                Set(dx, arch + 2, dz, BlockRegistry.AirId);
                            }
                        }
                    }
                    break;
                }

                case "field_bunker":
                {
                    for (int ry = 0; ry <= 4; ry++)
                        for (int dz = -3; dz <= 3; dz++)
                            for (int dx = -3; dx <= 3; dx++)
                            {
                                bool wall = Math.Abs(dx) == 3 || Math.Abs(dz) == 3;
                                if (ry == 0 || ry == 4) Set(dx, ry, dz, BlockRegistry.ReinforcedConcreteId);
                                else if (wall)
                                {
                                    bool door = dz == -3 && dx == 0 && ry <= 2;
                                    bool slit = ry == 2 && (dx == 0 || dz == 0);
                                    Set(dx, ry, dz, door ? BlockRegistry.AirId : slit ? BlockRegistry.ArmoredGlassId : BlockRegistry.ReinforcedConcreteId);
                                }
                                else Set(dx, ry, dz, BlockRegistry.AirId);
                            }
                    Set(-2, 1, 2, BlockRegistry.BedBlockId);
                    Set(2, 1, 2, BlockRegistry.WorkbenchId);
                    Set(0, 3, 0, BlockRegistry.ElectricFloodlightId);
                    break;
                }

                default: // cozy_cottage
                {
                    for (int ry = 0; ry <= 4; ry++)
                        for (int dz = -3; dz <= 3; dz++)
                            for (int dx = -3; dx <= 3; dx++)
                            {
                                bool wall = Math.Abs(dx) == 3 || Math.Abs(dz) == 3;
                                bool corner = Math.Abs(dx) == 3 && Math.Abs(dz) == 3;
                                if (ry == 0) Set(dx, 0, dz, BlockRegistry.PlanksId);
                                else if (ry <= 3)
                                {
                                    if (!wall) Set(dx, ry, dz, BlockRegistry.AirId);
                                    else if (dz == -3 && dx == 0 && ry <= 2) Set(dx, ry, dz, BlockRegistry.AirId);
                                    else if (ry == 2 && (dx == 0 || dz == 0)) Set(dx, ry, dz, 44);
                                    else Set(dx, ry, dz, corner ? BlockRegistry.CarvedOakId : (ushort)30);
                                }
                                else
                                    Set(dx, 4, dz, BlockRegistry.ShinglesId);
                            }
                    for (int dz = -2; dz <= 2; dz++)
                        for (int dx = -2; dx <= 2; dx++)
                            Set(dx, 5, dz, BlockRegistry.ShinglesId);
                    Set(-2, 1, 2, BlockRegistry.BedBlockId);
                    Set(2, 1, 2, BlockRegistry.WorkbenchId);
                    Set(2, 1, 1, BlockRegistry.FurnaceId);
                    Set(0, 3, 0, BlockRegistry.LanternBlockId);
                    break;
                }
            }
            return count;
        }

        // =====================================================================
        // LIVE VOXEL MINIMAP RENDERER (64x64 TEXTURE)
        // =====================================================================

        public static void RenderMinimap(Texture2D tex, int seed, Dimension dim, Vector3 playerPos, float playerYaw, List<WaypointEntry> waypoints)
        {
            if (!tex) return;
            int size = tex.width;
            int half = size / 2;
            const int step = 3; // 64 * 3 = 192m radius view!
            int px = Mathf.RoundToInt(playerPos.x);
            int pz = Mathf.RoundToInt(playerPos.z);

            var pixels = new Color32[size * size];
            for (int row = 0; row < size; row++)
            {
                int wz = pz + (row - half) * step;
                for (int col = 0; col < size; col++)
                {
                    int wx = px + (col - half) * step;
                    Color32 c;
                    if (dim == Dimension.Inferno)
                    {
                        int h = InfernoTerrain.FloorTop(wx, wz, seed);
                        if (h <= InfernoTerrain.LavaLevel)
                            c = new Color32(238, 86, 22, 235);
                        else
                        {
                            int b = InfernoTerrain.BiomeAt(wx, wz, seed);
                            byte shade = (byte)Mathf.Clamp(95 + (h - 28) * 3, 80, 220);
                            c = b == 0 ? new Color32(shade, (byte)(shade / 3), (byte)(shade / 4), 235)
                              : b == 1 ? new Color32((byte)(shade / 2), (byte)(shade / 2), (byte)(shade / 2 + 12), 235)
                              : b == 2 ? new Color32((byte)(shade * 3 / 4), (byte)(shade * 2 / 3), (byte)(shade / 2), 235)
                                       : new Color32(shade, (byte)(shade / 2), 28, 235);
                        }
                    }
                    else if (dim == Dimension.Paradise)
                    {
                        int h = ParadiseTerrain.FloorTop(wx, wz, seed);
                        if (h <= ParadiseTerrain.SeaLevel)
                            c = new Color32(72, 214, 245, 238);
                        else
                        {
                            int b = ParadiseTerrain.BiomeAt(wx, wz, seed);
                            byte v = (byte)Mathf.Clamp(150 + (h - 38) * 3, 140, 248);
                            c = b == 0 ? new Color32((byte)(v * 48 / 100), v, (byte)(v * 58 / 100), 238)
                              : b == 1 ? new Color32(v, (byte)(v * 96 / 100), (byte)(v * 84 / 100), 238)
                              : b == 2 ? new Color32((byte)(v * 82 / 100), (byte)(v * 68 / 100), v, 238)
                                       : new Color32(v, v, v, 238);
                        }
                    }
                    else if (dim == Dimension.Backrooms)
                    {
                        int zZone = BackroomsTerrain.ZoneAt(wx, wz, seed);
                        c = zZone == 1 ? new Color32(142, 224, 232, 235)
                          : zZone == 2 ? new Color32(128, 134, 140, 235)
                          : zZone == 3 ? new Color32(36, 40, 52, 235)
                          : zZone == 4 ? new Color32(198, 48, 44, 235)
                                       : new Color32(196, 174, 88, 235);
                    }
                    else if (dim == Dimension.Lestingia)
                    {
                        if (RealmWondersPlan.TryGetSkySurface(wx, wz, seed, out _))
                        {
                            c = new Color32(142, 236, 255, 240); // Floating Sky Island cyan highlight!
                        }
                        else
                        {
                            int h = TerrainGenerator.Height(wx, wz, seed, Dimension.Lestingia);
                            if (h <= 40)
                                c = new Color32(36, 128, 214, 235);
                            else if (h <= 42)
                                c = new Color32(226, 206, 138, 235);
                            else if (h >= 64)
                                c = new Color32(232, 242, 250, 235);
                            else
                            {
                                byte g = (byte)Mathf.Clamp(115 + (h - 40) * 4, 110, 215);
                                c = new Color32((byte)(g * 48 / 100), g, (byte)(g * 44 / 100), 235);
                            }
                        }
                    }
                    else
                    {
                        int h = TerrainGenerator.Height(wx, wz, seed, dim);
                        byte v = (byte)Mathf.Clamp(70 + h * 2, 60, 215);
                        c = new Color32(v, (byte)(v * 85 / 100), (byte)(v * 95 / 100), 235);
                    }
                    pixels[row * size + col] = c;
                }
            }

            Action<int, int, Color32, int> PlotDot = (worldX, worldZ, dotColor, radius) =>
            {
                int cx = half + Mathf.RoundToInt((worldX - px) / (float)step);
                int cy = half + Mathf.RoundToInt((worldZ - pz) / (float)step);
                cx = Mathf.Clamp(cx, 1, size - 2);
                cy = Mathf.Clamp(cy, 1, size - 2);
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (x >= 0 && x < size && y >= 0 && y < size)
                            pixels[y * size + x] = dotColor;
                    }
            };

            // Plot nearby Hellgate Shrine & Towns on Lestingia minimap
            if (dim == Dimension.Lestingia)
            {
                int tcx = Mathf.FloorToInt((float)px / RealmWondersPlan.TownCellSize);
                int tcz = Mathf.FloorToInt((float)pz / RealmWondersPlan.TownCellSize);
                for (int oz = -1; oz <= 1; oz++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (RealmWondersPlan.TryGetTownCenter(tcx + ox, tcz + oz, seed, out int tx, out _, out int tz, out _))
                            if (Mathf.Abs(tx - px) < half * step && Mathf.Abs(tz - pz) < half * step)
                                PlotDot(tx, tz, new Color32(255, 214, 64, 255), 2);

                int wcx = Mathf.FloorToInt((float)px / RealmWondersPlan.WonderCellSize);
                int wcz = Mathf.FloorToInt((float)pz / RealmWondersPlan.WonderCellSize);
                for (int oz = -1; oz <= 1; oz++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (RealmWondersPlan.TryGetWonderSite(wcx + ox, wcz + oz, seed, out int hx, out _, out int hz, out int wk) && wk == 4)
                            if (Mathf.Abs(hx - px) < half * step && Mathf.Abs(hz - pz) < half * step)
                                PlotDot(hx, hz, new Color32(216, 74, 255, 255), 2);
            }
            else if (dim == Dimension.Inferno)
            {
                InfernoTerrain.ExitPortal(px, pz, seed, out int ex, out int ez);
                PlotDot(ex, ez, new Color32(216, 74, 255, 255), 2);
                if (InfernoTerrain.TryGetCitadel(Mathf.FloorToInt(px / 192f), Mathf.FloorToInt(pz / 192f), seed, out int cx, out int cz))
                    PlotDot(cx, cz, new Color32(255, 196, 56, 255), 2);
            }
            else if (dim == Dimension.Paradise)
            {
                ParadiseTerrain.ExitPortal(px, pz, seed, out int ex, out int ez);
                PlotDot(ex, ez, new Color32(92, 240, 255, 255), 2);
                if (ParadiseTerrain.TryGetSanctuary(Mathf.FloorToInt(px / 192f), Mathf.FloorToInt(pz / 192f), seed, out int sx, out int sz))
                    PlotDot(sx, sz, new Color32(255, 226, 92, 255), 2);
            }
            else if (dim == Dimension.Backrooms)
            {
                BackroomsTerrain.ExitDoor(px, pz, seed, out int ex, out int ez);
                PlotDot(ex, ez, new Color32(120, 255, 190, 255), 2);
            }

            // Plot saved waypoints in current dimension
            if (waypoints != null)
            {
                foreach (var w in waypoints)
                {
                    if (w.dimension != (int)dim) continue;
                    Color32 wc = w.kind == "death" ? new Color32(255, 72, 72, 255)
                               : w.kind == "home" ? new Color32(82, 255, 138, 255)
                               : new Color32(255, 230, 92, 255);
                    PlotDot(w.x, w.z, wc, 1);
                }
            }

            // Player center marker + facing direction tick
            PlotDot(px, pz, new Color32(255, 255, 255, 255), 1);
            float rad = playerYaw * Mathf.Deg2Rad;
            int fx = half + Mathf.RoundToInt(Mathf.Sin(rad) * 3f);
            int fy = half + Mathf.RoundToInt(Mathf.Cos(rad) * 3f);
            if (fx >= 0 && fx < size && fy >= 0 && fy < size)
                pixels[fy * size + fx] = new Color32(255, 82, 64, 255);

            tex.SetPixels32(pixels);
            tex.Apply(false);
        }
    }
}
