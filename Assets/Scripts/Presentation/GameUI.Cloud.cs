using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Gameplay;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Wallet, cosmetics, marketplace, and perfected Friends & Player Card hub.
    /// Everything here is served by the game server: no Supabase URL or key is ever shipped to a player.
    /// </summary>
    public sealed partial class GameUI
    {
        string cloudNotice = "";
        string marketCode = "";
        string marketPrice = "";
        string marketSearch = "";
        int marketSort; // 0 = Новые, 1 = Дешевле, 2 = Дороже
        int marketTab = -1; // 0 = P2P Скины, 1 = Биржа Ресурсов, 2 = Чертежи [B], 3 = Контракты Гильдии
        int blueprintPage;
        string friendKey = "";
        string friendGreeting = "Привет! Пошли исследовать парящие острова!";
        string friendFilter = "";
        int friendsTab; // 0 = All friends, 1 = Online, 2 = Requests (incoming + outgoing)
        int friendsPage;
        string selectedFriendName = "";
        string selectedFriendKey = "";
        bool selectedFriendOnline;
        bool confirmRemoveFriend;

        AccountClient Accounts { get { return game ? game.Accounts : null; } }

        /// <summary>Re-renders whatever cloud screen is open after a request.</summary>
        void AfterCloud()
        {
            if (page == Page.Store) ShowStore();
            else if (page == Page.Market) ShowMarket();
            else if (page == Page.Friends) ShowFriends();
            else if (page == Page.Home) ShowHome();
        }

        void RequireLogin(Page target, string what)
        {
            Clear(target);
            Header(what, false);
            var card = Box(content.transform, 180, 176, 1240, 520, Panel);
            string localNick = ProfileAvatar.LocalNickname;
            AvatarPortrait(card.transform, ProfileAvatar.LocalAvatar, localNick, 48, 44, 112);
            Label(card.transform, "ЕДИНЫЙ ПРОФИЛЬ ДЛЯ ДРУЗЕЙ И КОШЕЛЬКА", 184, 44, 980, 30, 15, Accent, true);
            Label(card.transform, "@" + localNick, 184, 78, 900, 42, 30, White, true);
            Label(card.transform, "Список друзей, приглашения по @нику, баланс монет и коллекция скинов привязаны к вашему аккаунту LestingCraft.\nВойдите или зарегистрируйтесь за 10 секунд — без электронной почты и спама!",
                48, 186, 1140, 108, 18, Muted);

            var loginBtn = Button("Войти или создать аккаунт  →", 48, 336, 520, 64, ShowAccount, true, card.transform);
            loginBtn.name = "Cloud.Login";
            Button("← Назад в главное меню", 592, 336, 380, 64, () => { if (game && game.InWorld) ShowPause(); else ShowHome(); }, false, card.transform);
            Label(card.transform, "Локальный аватар и никнейм уже сохранены и автоматически подставятся при регистрации.", 48, 430, 1140, 32, 14, Accent);
        }

        public void ShowStore()
        {
            Clear(Page.Store);
            Header("Кошелёк и скины", false);
            var a = Accounts;
            if (a == null || !a.Logged) { RequireLogin(Page.Store, "Кошелёк и скины"); return; }
            if (a.Wallet == null && !a.CloudBusy) { a.CloudState(AfterCloud); }

            var wallet = a.Wallet;
            long balance = wallet != null ? wallet.balance : 0;
            var left = Box(content.transform, 64, 146, 470, 636, Panel);
            AvatarPortrait(left.transform, a.Profile != null ? a.Profile.avatar : null, a.Profile != null ? a.Profile.username : "Player", 26, 20, 64);
            Label(left.transform, "ВАШ БАЛАНС", 104, 20, 340, 22, 13, Accent, true);
            Label(left.transform, CloudFormat.Coins(balance) + " монет", 104, 44, 346, 42, 30, White, true);
            Box(left.transform, 26, 102, 418, 1, new Color(.8f, .9f, .8f, .16f), false);

            Label(left.transform, "ПОПОЛНЕНИЕ БАЛАНСА", 26, 118, 400, 24, 13, Accent, true);
            if (wallet == null || wallet.packs == null || wallet.packs.Length == 0)
                Label(left.transform, "Паки за реальные деньги появятся после подключения платёжного провайдера.\nЗаработать монеты можно продажей редких скинов на маркетплейсе игроков!",
                    26, 148, 420, 110, 15, Muted);
            else
                for (int i = 0; i < Mathf.Min(wallet.packs.Length, 4); i++)
                {
                    var pack = wallet.packs[i];
                    var row = Button("", 22, 148 + i * 62, 426, 54, () => { }, false, left.transform);
                    Label(row.transform, pack.title, 16, 6, 240, 24, 17, White, true);
                    Label(row.transform, pack.CoinsText + " монет", 16, 30, 240, 20, 13, Muted);
                    Label(row.transform, pack.PriceText, 268, 14, 140, 24, 16, Accent, false, TextAnchor.MiddleRight);
                }

            Label(left.transform, "ИСТОРИЯ ОПЕРАЦИЙ", 26, 418, 400, 24, 13, Accent, true);
            if (wallet == null || wallet.ledger == null || wallet.ledger.Length == 0)
                Label(left.transform, "Операций пока нет.", 26, 448, 400, 24, 15, Muted);
            else
                for (int i = 0; i < Mathf.Min(wallet.ledger.Length, 5); i++)
                {
                    var row = wallet.ledger[i];
                    Label(left.transform, CloudFormat.Reason(row.reason) + "   " + (row.delta > 0 ? "+" : "") + CloudFormat.Coins(row.delta),
                        26, 448 + i * 28, 418, 24, 15, row.delta > 0 ? SpringAccent : Muted);
                }

            var right = Box(content.transform, 562, 146, 974, 636, Panel);
            Label(right.transform, "МАГАЗИН ОБРАЗОВ И СКИНОВ", 28, 20, 600, 30, 17, Accent, true);
            Label(right.transform, "Купленный образ можно надеть на персонажа — он отображается друзьям и в мультиплеере.", 28, 50, 900, 24, 14, Muted);
            var owned = wallet != null ? wallet.owned : null;
            var store = wallet != null ? wallet.store : null;
            if (store == null || store.Length == 0)
                Label(right.transform, "Каталог магазина загружается…", 28, 96, 700, 30, 16, Muted);
            for (int i = 0; store != null && i < Mathf.Min(store.Length, 6); i++)
            {
                var item = store[i];
                bool has = false;
                if (owned != null) foreach (var mine in owned) if (mine.code == item.code) has = true;
                bool worn = wallet != null && wallet.profile != null && wallet.profile.avatar_skin == item.code;
                var row = Box(right.transform, 24, 92 + i * 86, 926, 76, new Color(.05f, .13f, .21f, .92f));
                Label(row.transform, item.title, 20, 10, 460, 26, 19, White, true);
                Label(row.transform, CloudFormat.Kind(item.kind) + "  ·  " + (item.description ?? ""), 20, 40, 470, 26, 14, Muted);
                string action = worn ? "✓ Надето" : has ? "Надеть" : CloudFormat.Coins(item.price) + " монет";
                var button = Button(action, 640, 12, 262, 52, () =>
                {
                    if (worn) return;
                    if (has) a.CloudEquip(item.code, AfterCloud);
                    else a.CloudBuy(item.code, AfterCloud);
                }, !worn && !has, row.transform);
                button.name = "Store." + item.code;
            }

            Button("← В главное меню", 64, 804, 250, 52, () => { cloudNotice = ""; if (game && game.InWorld) ShowPause(); else ShowHome(); });
            Button("Маркетплейс и чертежи  →", 330, 804, 360, 52, () => { a.CloudMarket(AfterCloud); ShowMarketTab(0); }, true);
            Button("Друзья  →", 706, 804, 240, 52, () => { a.CloudFriends(AfterCloud); ShowFriends(); });
            Button("Мой профиль", 962, 804, 240, 52, () => { cloudNotice = ""; ShowAccount(); });
            status = Label(content.transform, !string.IsNullOrEmpty(cloudNotice) ? cloudNotice : string.IsNullOrEmpty(a.CloudMessage) ? "" : a.CloudMessage,
                1216, 804, 320, 52, 13, Accent, false, TextAnchor.MiddleRight);
        }

        public void ShowMarketTab(int tab)
        {
            marketTab = Mathf.Clamp(tab, 0, 4);
            ShowMarket();
        }

        public void ShowMarket()
        {
            Clear(Page.Market);
            Header("Торговая Площадь и Маркетплейс", false);
            var a = Accounts;
            bool logged = a != null && a.Logged;
            if (marketTab < 0) marketTab = logged ? 0 : 1;
            if (logged && marketTab == 0 && a.Board == null && !a.CloudBusy) a.CloudMarket(AfterCloud);

            // Top Wallet + 5 Category Tabs Bar
            var topBar = Box(content.transform, 64, 128, 1472, 66, Panel);
            string tab0Name = "1. P2P-Скины";
            string tab1Name = "2. Биржа Ресурсов";
            string tab2Name = "3. Чертежи [B]";
            string tab3Name = "4. Контракты";
            string tab4Name = "5. Ритуалы и Миры";

            var t0 = Button(tab0Name, 12, 9, 206, 48, () => { marketTab = 0; cloudNotice = ""; ShowMarket(); }, marketTab == 0, topBar.transform);
            t0.name = "Market.Tab.0";
            var t1 = Button(tab1Name, 224, 9, 224, 48, () => { marketTab = 1; cloudNotice = ""; ShowMarket(); }, marketTab == 1, topBar.transform);
            t1.name = "Market.Tab.1";
            var t2 = Button(tab2Name, 454, 9, 214, 48, () => { marketTab = 2; cloudNotice = ""; ShowMarket(); }, marketTab == 2, topBar.transform);
            t2.name = "Market.Tab.2";
            var t3 = Button(tab3Name, 674, 9, 198, 48, () => { marketTab = 3; cloudNotice = ""; ShowMarket(); }, marketTab == 3, topBar.transform);
            t3.name = "Market.Tab.3";
            var t4 = Button(tab4Name, 878, 9, 232, 48, () => { marketTab = 4; cloudNotice = ""; ShowMarket(); }, marketTab == 4, topBar.transform);
            t4.name = "Market.Tab.4";

            string cloudCoinsText = logged && a.Wallet != null ? ("Облако: " + CloudFormat.Coins(a.Wallet.balance) + " м.  ·  ") : "";
            Label(topBar.transform, cloudCoinsText + "Монеты Гильдии: " + RealmFeaturesAndMarket.GuildCoins,
                1118, 18, 336, 30, 15, SpringAccent, true, TextAnchor.MiddleRight);

            if (marketTab == 0) DrawMarketCloudTab(a, logged);
            else if (marketTab == 1) DrawMarketResourcesTab();
            else if (marketTab == 2) DrawMarketBlueprintsTab();
            else if (marketTab == 3) DrawMarketContractsTab();
            else DrawMarketRitualsTab();

            // Bottom Navigation Bar
            Button(game && game.InWorld ? "← Вернуться в мир" : "← В главное меню", 64, 804, 290, 52, () =>
            {
                cloudNotice = "";
                if (game && game.InWorld) ShowPause();
                else ShowHome();
            });
            Button("Кошелёк и скины", 368, 804, 260, 52, () => { cloudNotice = ""; ShowStore(); });
            if (marketTab == 0 && logged)
                Button("Обновить лоты", 642, 804, 220, 52, () => a.CloudMarket(AfterCloud));

            string msg = !string.IsNullOrEmpty(cloudNotice)
                ? cloudNotice
                : (logged && !string.IsNullOrEmpty(a.CloudMessage) ? a.CloudMessage : "Маркетплейс работает и онлайн (P2P-скины), и в любом мире (ресурсы, 3D-чертежи [B], контракты).");
            status = Label(content.transform, msg, 64, 756, 1472, 34, 15, Accent);
        }

        void DrawMarketCloudTab(AccountClient a, bool logged)
        {
            if (!logged)
            {
                var card = Box(content.transform, 280, 226, 1040, 480, Panel);
                Label(card.transform, "P2P-МАРКЕТПЛЕЙС КОСМЕТИКИ И СКИНОВ", 44, 34, 920, 36, 24, White, true);
                Label(card.transform, "На вкладке P2P игроки торгуют редкими плащами, коронами и обликами через защищённый серверный эскроу.\n\nВойдите в облачный аккаунт, чтобы покупать и выставлять скины — или переключитесь на вкладки «Биржа Ресурсов», «Чертежи Построек [B]» и «Контракты Гильдии» сверху (они доступны мгновенно даже оффлайн!).",
                    44, 92, 952, 180, 17, Muted);
                Button("Войти или создать аккаунт  →", 44, 310, 420, 62, ShowAccount, true, card.transform);
                Button("Перейти к Бирже Ресурсов  →", 484, 310, 420, 62, () => ShowMarketTab(1), false, card.transform);
                return;
            }

            var board = a.Board;
            var wallet = a.Wallet;

            var left = Box(content.transform, 64, 206, 940, 538, Panel);
            Label(left.transform, "ЛОТЫ ИГРОКОВ (ЗАЩИЩЁННЫЙ ЭСКРОУ · КОМИССИЯ 5%)", 24, 14, 620, 26, 16, Accent, true);

            // Search & Sort Controls
            InputIn(left.transform, "", marketSearch, "Поиск по названию или продавцу…", 24, 32, 430, v => { marketSearch = v; }, 32);
            Button("Найти", 464, 46, 108, 42, ShowMarket, false, left.transform);
            Button("Новые", 584, 46, 104, 42, () => { marketSort = 0; ShowMarket(); }, marketSort == 0, left.transform);
            Button("Цена ↑", 694, 46, 104, 42, () => { marketSort = 1; ShowMarket(); }, marketSort == 1, left.transform);
            Button("Цена ↓", 804, 46, 108, 42, () => { marketSort = 2; ShowMarket(); }, marketSort == 2, left.transform);

            var filtered = new List<CloudListing>();
            if (board != null && board.listings != null)
            {
                string q = (marketSearch ?? "").Trim().ToLowerInvariant();
                for (int i = 0; i < board.listings.Length; i++)
                {
                    var lot = board.listings[i];
                    if (lot == null) continue;
                    if (q.Length > 0)
                    {
                        string t = (lot.title ?? lot.item ?? "").ToLowerInvariant();
                        string s = (lot.seller ?? "").ToLowerInvariant();
                        if (!t.Contains(q) && !s.Contains(q)) continue;
                    }
                    filtered.Add(lot);
                }
            }
            if (marketSort == 1) filtered.Sort((x, y) => x.price.CompareTo(y.price));
            else if (marketSort == 2) filtered.Sort((x, y) => y.price.CompareTo(x.price));

            if (filtered.Count == 0)
                Label(left.transform, "Подходящих активных лотов пока нет. Выставьте свой свободный скин справа!", 26, 112, 860, 30, 16, Muted);

            for (int i = 0; i < Mathf.Min(filtered.Count, 5); i++)
            {
                var lot = filtered[i];
                var row = Box(left.transform, 22, 102 + i * 82, 896, 72, new Color(.05f, .13f, .21f, .92f));
                Label(row.transform, lot.title ?? lot.item, 18, 9, 430, 26, 18, White, true);
                Label(row.transform, "@" + (lot.seller ?? "Игрок") + "  ·  " + CloudFormat.Kind(lot.kind), 18, 38, 430, 22, 14, Muted);
                Label(row.transform, CloudFormat.Coins(lot.price) + " монет", 470, 22, 200, 26, 17, Accent, false, TextAnchor.MiddleRight);
                if (lot.mine)
                    Button("Снять лот", 690, 11, 186, 50, () => a.CloudCancel(lot.id, AfterCloud), false, row.transform);
                else
                {
                    var buy = Button("Купить", 690, 11, 186, 50, () => a.CloudPurchase(lot.id, AfterCloud), true, row.transform);
                    buy.name = "Market.buy." + lot.id;
                }
            }

            var right = Box(content.transform, 1024, 206, 512, 538, Panel);
            Label(right.transform, "ПРОДАТЬ СВОЙ СКИН", 24, 16, 460, 28, 17, Accent, true);
            var owned = wallet != null ? wallet.owned : null;
            if (owned == null || owned.Length == 0)
                Label(right.transform, "У вас пока нет свободных скинов.\nЗагляните в «Кошелёк и скины», чтобы приобрести облик (надетый скин продать нельзя).", 24, 56, 464, 96, 15, Muted);
            else
                for (int i = 0; i < Mathf.Min(owned.Length, 4); i++)
                {
                    var item = owned[i];
                    bool chosen = marketCode == item.code;
                    var row = Button("", 20, 54 + i * 58, 472, 50, () => { marketCode = item.code; ShowMarket(); }, chosen, right.transform);
                    Label(row.transform, item.title, 16, 6, 430, 22, 16, chosen ? Ink : White, true);
                    Label(row.transform, CloudFormat.Kind(item.kind), 16, 27, 300, 18, 12, chosen ? Ink : Muted);
                }

            InputIn(right.transform, "ЦЕНА В МОНЕТАХ", marketPrice, "Например 250", 24, 300, 214, v => marketPrice = v, 10);
            long previewPrice;
            if (long.TryParse((marketPrice ?? "").Trim(), out previewPrice) && previewPrice > 0)
            {
                long net = Math.Max(1L, previewPrice - (previewPrice * 5L) / 100L);
                Label(right.transform, "Вы получите чистыми (после 5%): " + CloudFormat.Coins(net) + " монет", 24, 392, 464, 24, 13, SpringAccent);
            }

            var listButton = Button("Выставить лот", 252, 324, 236, 52, () =>
            {
                long price;
                if (string.IsNullOrEmpty(marketCode)) { cloudNotice = "Сначала выберите скин в списке."; ShowMarket(); return; }
                if (!long.TryParse((marketPrice ?? "").Trim(), out price) || price <= 0) { cloudNotice = "Цена должна быть целым числом больше нуля."; ShowMarket(); return; }
                cloudNotice = "";
                a.CloudList(marketCode, price, AfterCloud);
                marketCode = ""; marketPrice = "";
            }, true, right.transform);
            listButton.name = "Market.list";

            if (wallet != null && wallet.listings != null && wallet.listings.Length > 0)
                Label(right.transform, "Ваших активных лотов на витрине: " + wallet.listings.Length, 24, 430, 460, 26, 15, Accent);
        }

        void DrawMarketResourcesTab()
        {
            var inv = game && game.Session != null ? game.Session.Inventory : null;
            bool hasInv = game && game.InWorld && inv != null;

            // Left: Sell Mined Resources for Guild Coins
            var left = Box(content.transform, 64, 206, 720, 538, Panel);
            Label(left.transform, "СКУПКА РЕСУРСОВ КАРАВАНОМ (+МОНЕТЫ ГИЛЬДИИ)", 24, 14, 660, 26, 16, SpringAccent, true);
            Label(left.transform, hasInv
                ? "Продавайте излишки дерева, руды и адских кристаллов прямо из вашего рюкзака!"
                : "Зайдите в любой мир (Одиночная игра или Сервер), чтобы продавать ресурсы из рюкзака.", 24, 42, 672, 24, 13, Muted);

            for (int i = 0; i < RealmFeaturesAndMarket.SellOffers.Length; i++)
            {
                var offer = RealmFeaturesAndMarket.SellOffers[i];
                int inBag = hasInv ? inv.Count(offer.ItemId) : 0;
                var row = Box(left.transform, 20, 76 + i * 74, 680, 66, new Color(.05f, .14f, .21f, .92f));
                Label(row.transform, offer.Title, 16, 7, 420, 24, 16, White, true);
                Label(row.transform, offer.Subtitle + (hasInv ? "  ·  В сумке: " + inBag : ""), 16, 33, 440, 22, 12, Muted);
                bool canSell = hasInv && inBag >= offer.Count;
                var btn = Button("+" + offer.CoinValue + " монет", 488, 10, 176, 46, () =>
                {
                    if (!hasInv) { cloudNotice = "Сначала войдите в игровой мир, чтобы торговать содержимым инвентаря!"; ShowMarket(); return; }
                    RealmFeaturesAndMarket.TryExecuteTrade(offer, inv, out string msg);
                    cloudNotice = msg;
                    ShowMarket();
                }, canSell, row.transform);
                btn.name = "Market.Sell." + offer.Code;
            }

            // Right: Buy Expedition Kits, Diamond Tools & Hellgate Kit
            var right = Box(content.transform, 808, 206, 728, 538, Panel);
            Label(right.transform, "ЭКСПЕДИЦИОННОЕ СНАБЖЕНИЕ И АРТЕФАКТЫ", 24, 14, 660, 26, 16, Accent, true);
            Label(right.transform, "Покупайте инструменты, редкие блоки и готовый набор Врат Ада за Монеты Гильдии.", 24, 42, 680, 24, 13, Muted);

            for (int i = 0; i < RealmFeaturesAndMarket.BuySupplies.Length; i++)
            {
                var offer = RealmFeaturesAndMarket.BuySupplies[i];
                var row = Box(right.transform, 20, 76 + i * 74, 688, 66, new Color(.05f, .14f, .21f, .92f));
                Label(row.transform, offer.Title, 16, 7, 430, 24, 16, White, true);
                Label(row.transform, offer.Subtitle, 16, 33, 440, 22, 12, Muted);
                bool canBuy = hasInv && RealmFeaturesAndMarket.GuildCoins >= offer.CoinValue;
                var btn = Button(offer.CoinValue + " монет", 494, 10, 178, 46, () =>
                {
                    if (!hasInv) { cloudNotice = "Войдите в игровой мир, чтобы получить купленный набор прямо в инвентарь!"; ShowMarket(); return; }
                    RealmFeaturesAndMarket.TryExecuteTrade(offer, inv, out string msg);
                    cloudNotice = msg;
                    ShowMarket();
                }, canBuy, row.transform);
                btn.name = "Market.BuySupply." + offer.Code;
            }
        }

        void DrawMarketBlueprintsTab()
        {
            var panel = Box(content.transform, 64, 206, 1472, 538, Panel);
            Label(panel.transform, "3D-ЧЕРТЕЖИ АРХИТЕКТОРА (МГНОВЕННАЯ ПОСТРОЙКА ПО КЛАВИШЕ [B])", 26, 14, 980, 28, 17, Accent, true);
            Label(panel.transform, "Откройте чертёж один раз — и он навсегда доступен во всех мирах! В игре нажмите [B], чтобы возвести выбранную постройку перед собой ([Shift+B] — переключить чертёж).",
                26, 42, 1380, 24, 14, Muted);

            string activeCode = RealmFeaturesAndMarket.ActiveBlueprintCode;
            var all = RealmFeaturesAndMarket.Blueprints;

            for (int i = 0; i < all.Length; i++)
            {
                var bp = all[i];
                int col = i % 2;
                int rowIdx = i / 2;
                float x = 24 + col * 720;
                float y = 78 + rowIdx * 110;
                bool unlocked = RealmFeaturesAndMarket.IsBlueprintUnlocked(bp.Code);
                bool equipped = unlocked && bp.Code == activeCode;

                var card = Box(panel.transform, x, y, 704, 100, equipped ? new Color(.08f, .25f, .22f, .94f) : new Color(.05f, .13f, .21f, .92f));
                Label(card.transform, bp.Title + "  ·  " + bp.SizeHint, 18, 10, 450, 26, 17, equipped ? SpringAccent : White, true);
                Label(card.transform, bp.Category + "  —  " + bp.Description, 18, 38, 450, 48, 13, Muted);

                if (!unlocked)
                {
                    var buyBtn = Button("Открыть · " + bp.PriceCoins + " м.", 486, 22, 202, 54, () =>
                    {
                        if (RealmFeaturesAndMarket.GuildCoins < bp.PriceCoins)
                        {
                            cloudNotice = "Недостаточно Монет Гильдии (" + RealmFeaturesAndMarket.GuildCoins + " / " + bp.PriceCoins + "). Выполните Контракт или продайте ресурсы!";
                            ShowMarket();
                            return;
                        }
                        RealmFeaturesAndMarket.GuildCoins -= bp.PriceCoins;
                        RealmFeaturesAndMarket.UnlockBlueprint(bp.Code);
                        RealmFeaturesAndMarket.ActiveBlueprintCode = bp.Code;
                        RealmFeaturesAndMarket.AddContractProgress("market_trader", 1);
                        cloudNotice = "Чертёж «" + bp.Title + "» открыт и выбран для клавиши [B]!";
                        ShowMarket();
                    }, RealmFeaturesAndMarket.GuildCoins >= bp.PriceCoins, card.transform);
                    buyBtn.name = "Market.Blueprint." + bp.Code;
                }
                else
                {
                    Button(equipped ? "✓ Активен [B]" : "Выбрать для [B]", 486, 10, 202, 38, () =>
                    {
                        RealmFeaturesAndMarket.ActiveBlueprintCode = bp.Code;
                        cloudNotice = "Активный чертёж [B]: «" + bp.Title + "».";
                        ShowMarket();
                    }, !equipped, card.transform);

                    if (game && game.InWorld)
                    {
                        Button("Построить сейчас!", 486, 54, 202, 38, () =>
                        {
                            RealmFeaturesAndMarket.ActiveBlueprintCode = bp.Code;
                            game.Resume();
                            game.PlaceActiveBlueprintNearPlayer();
                        }, true, card.transform);
                    }
                }
            }
        }

        void DrawMarketContractsTab()
        {
            var panel = Box(content.transform, 64, 206, 1472, 538, Panel);
            Label(panel.transform, "ДОСКА КОНТРАКТОВ И ПОРУЧЕНИЙ ГИЛЬДИИ", 26, 14, 900, 28, 17, SpringAccent, true);
            Label(panel.transform, "Выполняйте поручения во время приключений — получайте Монеты Гильдии для чертежей и редкие алмазы в рюкзак!",
                26, 42, 1380, 24, 14, Muted);

            var inv = game && game.Session != null && game.InWorld ? game.Session.Inventory : null;
            var list = RealmFeaturesAndMarket.Contracts;

            for (int i = 0; i < list.Length; i++)
            {
                var c = list[i];
                int col = i % 2;
                int rowIdx = i / 2;
                float x = 24 + col * 720;
                float y = 74 + rowIdx * 112;
                int prog = RealmFeaturesAndMarket.GetContractProgress(c.Id);
                bool claimed = RealmFeaturesAndMarket.IsContractClaimed(c.Id);
                bool ready = prog >= c.Target && !claimed;

                var card = Box(panel.transform, x, y, 704, 104, claimed ? new Color(.05f, .18f, .16f, .86f) : new Color(.05f, .13f, .21f, .92f));
                Label(card.transform, c.Title, 18, 8, 440, 24, 17, claimed ? SpringAccent : White, true);
                Label(card.transform, c.Description, 18, 34, 450, 36, 12, Muted);
                Label(card.transform, "Прогресс: " + prog + " / " + c.Target + "   ·   Награда: +" + c.RewardCoins + " монет",
                    18, 72, 450, 24, 13, ready ? SpringAccent : Accent, true);

                string btnText = claimed ? "✓ Получено" : (ready ? "Забрать +" + c.RewardCoins + " м.!" : "В процессе");
                var claimBtn = Button(btnText, 488, 26, 198, 52, () =>
                {
                    if (!ready) return;
                    RealmFeaturesAndMarket.TryClaimContract(c.Id, inv, out string msg);
                    cloudNotice = msg;
                    ShowMarket();
                }, ready, card.transform);
                claimBtn.name = "Market.Contract." + c.Id;
            }
        }

        void DrawMarketRitualsTab()
        {
            var panel = Box(content.transform, 64, 206, 1472, 538, Panel);
            Label(panel.transform, "КОДЕКС РИТУАЛОВ, ИЗМЕРЕНИЙ И КОМАНД НОВОЙ ЭРЫ", 26, 14, 1080, 28, 17, SpringAccent, true);
            Label(panel.transform, "Никаких чужих шаблонов: у каждого скрытого измерения в LestingCraft собственный авторский ритуал, экосистема и страж!",
                26, 42, 1400, 24, 14, Muted);

            // Card 1: Hell Twin-Tree Ritual
            var cHell = Box(panel.transform, 24, 78, 704, 214, new Color(.18f, .06f, .05f, .92f));
            Label(cHell.transform, "🔥 1. Ритуал Ада — Пылающая Арка Двух Деревьев", 18, 12, 668, 28, 18, new Color(1f, .62f, .28f), true);
            Label(cHell.transform,
                "• Посадите 2 саженца (дуба #92 или сакуры #93) на расстоянии РОВНО 7 блоков друг от друга.\n" +
                "• Дождитесь их естественного роста БЕЗ костной муки (ускоренные мукой деревья не образуют арку!).\n" +
                "• Подожгите оба дерева факелом (#148) или костром (#15) [ПКМ по стволу].\n" +
                "• Когда кроны прогорят и стволы обуглятся, ветви сомкнутся в Огненную Арку — шагните под неё!",
                18, 46, 668, 114, 13, White);
            Button("Купить саженцы на Бирже →", 18, 162, 290, 40, () => { marketTab = 1; ShowMarket(); }, false, cHell.transform);

            // Card 2: Paradise Flower-Water-Diamond Well Ritual
            var cEden = Box(panel.transform, 744, 78, 704, 214, new Color(.06f, .16f, .24f, .92f));
            Label(cEden.transform, "✨ 2. Ритуал Рая — Цветочный Колодец Эдема", 18, 12, 668, 28, 18, new Color(.62f, .94f, 1f), true);
            Label(cEden.transform,
                "• Выкопайте в земле квадратный колодец 2×2 шириной и 5 блоков глубиной.\n" +
                "• Наполните колодец чистой водой (#7 — вода автоматически заполнит всю шахту 2×2×5).\n" +
                "• Высадите любые цветы (#94 одуванчик, #95 мак, #96 василёк, #97 лаванда) вокруг кромки.\n" +
                "• Бросьте в воду Алмаз (#149) клавишей [Q] (или ПКМ) и прыгните в засиявший колодец!",
                18, 46, 668, 114, 13, White);
            Button("Купить цветы Эдема на Бирже →", 18, 162, 290, 40, () => { marketTab = 1; ShowMarket(); }, false, cEden.transform);

            // Card 3: Backrooms 5 Sublevels & 42 Wildlife Species
            var cBack = Box(panel.transform, 24, 306, 704, 210, new Color(.14f, .13f, .06f, .92f));
            Label(cBack.transform, "🚪 3. Закулисье (5 Подуровней) и 42 Вида Фауны", 18, 12, 668, 28, 18, Accent, true);
            Label(cBack.transform,
                "• Закулисье включает 5 секторов: Уровень 0 «Жёлтое лобби», Уровень 37 «Бассейны» (Poolrooms),\n" +
                "  Уровень 1 «Обитаемый склад», Уровень 6 «Полная темнота» и Уровень «!» «Аварийный сектор».\n" +
                "• Все миры заселены живой 3D-фауной (42 вида): гончие и страйдеры Ада, пегасы, лебеди\n" +
                "  и жар-птицы Эдема, небесные левиафаны экзопланет и 3 подтипа сталкеров Закулисья.",
                18, 46, 668, 146, 13, White);

            // Card 4: Commands & Creative Flight
            var cCmd = Box(panel.transform, 744, 306, 704, 210, new Color(.06f, .13f, .20f, .92f));
            Label(cCmd.transform, "🕊 4. Полёт в Креативе и Чат-Команды", 18, 12, 668, 28, 18, SpringAccent, true);
            Label(cCmd.transform,
                "• /gamemode <creative | survival | hardcore | story> (или /gm 0..3) — смена режима на лету.\n" +
                "• Двойной [Пробел] или /fly в Творческом режиме — свободный 3D-полёт ([Shift] — ускорение, [Ctrl/C] — вниз).\n" +
                "• Быстрые переходы (в креативе/отладке): /paradise (/рай), /hell (/ад), /backrooms, /overworld.\n" +
                "• Навигация и сеть: /sethome, /home, /wp <имя>, /lan (открыть локальный мир по Wi-Fi).",
                18, 46, 668, 146, 13, White);
        }

        public void ShowFriends()
        {
            Clear(Page.Friends);
            Header("Друзья и команда", false);
            var a = Accounts;
            if (a == null || !a.Logged) { RequireLogin(Page.Friends, "Друзья и команда"); return; }
            if (a.Friends == null && !a.CloudBusy) a.CloudFriends(AfterCloud);
            if (a.Wallet == null && !a.CloudBusy) a.CloudState(AfterCloud);

            var reply = a.Friends;
            var allFriends = new List<CloudFriend>();
            int onlineTotal = 0;
            if (reply != null && reply.friends != null)
            {
                foreach (var f in reply.friends)
                {
                    if (f == null) continue;
                    if (f.online) onlineTotal++;
                    allFriends.Add(f);
                }
                // Online friends always appear first, then alphabetical!
                allFriends.Sort((x, y) =>
                {
                    if (x.online != y.online) return x.online ? -1 : 1;
                    return string.Compare(x.name ?? "", y.name ?? "", StringComparison.OrdinalIgnoreCase);
                });
            }

            int incCount = reply != null && reply.incoming != null ? reply.incoming.Length : 0;
            int outCount = reply != null && reply.outgoing != null ? reply.outgoing.Length : 0;
            int reqTotal = incCount + outCount;

            // 1. Top Identity & Quick-Share Bar
            var topBar = Box(content.transform, 64, 132, 1472, 68, Panel);
            string myNick = a.Profile != null ? a.Profile.username : ProfileAvatar.LocalNickname;
            string myAv = a.Profile != null ? a.Profile.avatar : ProfileAvatar.LocalAvatar;
            AvatarPortrait(topBar.transform, myAv, myNick, 14, 10, 48);
            Label(topBar.transform, "ВАШ НИК ДЛЯ ДРУЗЕЙ", 74, 10, 260, 18, 11, Accent, true);
            Label(topBar.transform, "@" + myNick, 74, 28, 300, 30, 22, White, true);
            Button("Скопировать @ник", 350, 13, 210, 42, () =>
            {
                GUIUtility.systemCopyBuffer = myNick;
                cloudNotice = "Ваш ник @" + myNick + " скопирован — отправьте его другу!";
                ShowFriends();
            }, false, topBar.transform);

            string summaryText = "В сети: " + onlineTotal + " из " + allFriends.Count + "   ·   Входящих заявок: " + incCount + (outCount > 0 ? "   ·   Отправлено: " + outCount : "");
            Label(topBar.transform, summaryText, 584, 22, 640, 26, 15, onlineTotal > 0 ? SpringAccent : Muted);
            var refreshBtn = Button("Обновить ↻", 1296, 13, 160, 42, () => { cloudNotice = ""; a.CloudFriends(AfterCloud); }, false, topBar.transform);
            refreshBtn.name = "Friends.Refresh";

            // 2. Left Panel: Friends List, Search & Tabs
            var left = Box(content.transform, 64, 212, 720, 568, Panel);
            Button("Все (" + allFriends.Count + ")", 20, 16, 212, 42, () => { friendsTab = 0; friendsPage = 0; ShowFriends(); }, friendsTab == 0, left.transform);
            Button("В сети (" + onlineTotal + ")", 244, 16, 212, 42, () => { friendsTab = 1; friendsPage = 0; ShowFriends(); }, friendsTab == 1, left.transform);
            Button("Заявки (" + reqTotal + ")", 468, 16, 232, 42, () => { friendsTab = 2; friendsPage = 0; ShowFriends(); }, friendsTab == 2, left.transform);

            if (friendsTab != 2)
            {
                InputIn(left.transform, "ПОИСК В СПИСКЕ ДРУЗЕЙ", friendFilter, "Введите часть @ника…", 20, 68, 536, v => friendFilter = v, 24);
                Button("Найти", 568, 94, 132, 50, () => { friendsPage = 0; ShowFriends(); }, false, left.transform);

                var filtered = new List<CloudFriend>();
                string needle = (friendFilter ?? "").Trim().TrimStart('@').ToLowerInvariant();
                foreach (var f in allFriends)
                {
                    if (friendsTab == 1 && !f.online) continue;
                    string fn = (f.name ?? "").ToLowerInvariant();
                    if (!string.IsNullOrEmpty(needle) && !fn.Contains(needle)) continue;
                    filtered.Add(f);
                }

                if (reply == null)
                {
                    Label(left.transform, "Загружаем список друзей…", 24, 176, 660, 32, 16, Muted);
                }
                else if (filtered.Count == 0)
                {
                    string emptyMsg = allFriends.Count == 0
                        ? "Список друзей пока пуст.\nВведите @ник друга в панели справа, чтобы отправить приглашение!"
                        : (friendsTab == 1 ? "Сейчас никто из друзей не в сети." : "По запросу «" + needle + "» друзей не найдено.");
                    Label(left.transform, emptyMsg, 24, 176, 668, 96, 16, Muted);
                }
                else
                {
                    const int perPage = 5;
                    int totalPages = Mathf.Max(1, (filtered.Count + perPage - 1) / perPage);
                    friendsPage = Mathf.Clamp(friendsPage, 0, totalPages - 1);
                    for (int i = 0; i < perPage; i++)
                    {
                        int idx = friendsPage * perPage + i;
                        if (idx >= filtered.Count) break;
                        var friend = filtered[idx];
                        string fname = string.IsNullOrEmpty(friend.name) ? CloudFormat.ShortKey(friend.key) : friend.name;
                        AccountCard cachedCard = null;
                        if (!string.IsNullOrEmpty(friend.name)) a.Cards.TryGetValue(friend.name, out cachedCard);
                        string fav = cachedCard != null ? cachedCard.avatar : null;
                        bool isSelected = !string.IsNullOrEmpty(selectedFriendName) && string.Equals(selectedFriendName, friend.name, StringComparison.OrdinalIgnoreCase);

                        var row = Box(left.transform, 20, 156 + i * 72, 680, 64, isSelected ? new Color(.11f, .27f, .40f, .96f) : new Color(.05f, .13f, .21f, .92f));
                        AvatarPortrait(row.transform, fav, fname, 10, 8, 48);
                        var statusDot = Box(row.transform, 48, 46, 12, 12, friend.online ? SpringAccent : new Color(.45f, .55f, .65f, .9f));
                        statusDot.raycastTarget = false;

                        Label(row.transform, "@" + fname, 70, 8, 320, 26, 18, White, true);
                        string sub = (friend.online ? "● В игре" : "○ Не в сети");
                        if (cachedCard != null) sub += "  ·  Ур. " + cachedCard.level + " · " + cachedCard.titleName;
                        else if (!string.IsNullOrEmpty(friend.skin)) sub += "  ·  образ " + friend.skin;
                        Label(row.transform, sub, 70, 34, 340, 22, 13, friend.online ? SpringAccent : Muted);

                        Button("Профиль", 422, 10, 122, 44, () => InspectFriend(friend), isSelected, row.transform);
                        Button("Звать", 552, 10, 114, 44, () => InviteFriendToCoop(fname), false, row.transform);
                    }

                    if (totalPages > 1)
                    {
                        Button("←", 240, 520, 72, 38, () => { friendsPage--; ShowFriends(); }, false, left.transform);
                        Label(left.transform, (friendsPage + 1) + " / " + totalPages, 320, 524, 80, 30, 15, White, false, TextAnchor.MiddleCenter);
                        Button("→", 408, 520, 72, 38, () => { friendsPage++; ShowFriends(); }, false, left.transform);
                    }
                }
            }
            else
            {
                // Requests Tab: Full Incoming + Outgoing Management!
                int yCursor = 76;
                Label(left.transform, "ВХОДЯЩИЕ ЗАЯВКИ (" + incCount + ")", 22, yCursor, 660, 24, 14, Accent, true);
                yCursor += 30;
                if (incCount == 0)
                {
                    Label(left.transform, "Новых входящих заявок нет.", 22, yCursor, 660, 26, 15, Muted);
                    yCursor += 42;
                }
                else
                {
                    for (int i = 0; i < Mathf.Min(incCount, 3); i++)
                    {
                        var req = reply.incoming[i];
                        long id = req.id;
                        string rname = string.IsNullOrEmpty(req.name) ? "player" : req.name;
                        var row = Box(left.transform, 20, yCursor, 680, 68, new Color(.05f, .13f, .21f, .92f));
                        AvatarPortrait(row.transform, null, rname, 10, 10, 48);
                        Label(row.transform, "@" + rname, 68, 8, 360, 24, 17, White, true);
                        Label(row.transform, string.IsNullOrEmpty(req.message) ? "Хочет добавить вас в друзья" : req.message, 68, 34, 368, 24, 13, Muted);
                        Button("Принять", 448, 11, 112, 46, () => a.CloudFriendAccept(id, AfterCloud), true, row.transform);
                        Button("Отклонить", 568, 11, 100, 46, () => a.CloudFriendDecline(id, AfterCloud), false, row.transform);
                        yCursor += 76;
                    }
                }

                yCursor += 10;
                Label(left.transform, "ОТПРАВЛЕННЫЕ ЗАЯВКИ (" + outCount + ")", 22, yCursor, 660, 24, 14, Accent, true);
                yCursor += 30;
                if (outCount == 0)
                {
                    Label(left.transform, "Нет ожидающих отправленных заявок.", 22, yCursor, 660, 26, 15, Muted);
                }
                else
                {
                    for (int i = 0; i < Mathf.Min(outCount, 3) && yCursor <= 492; i++)
                    {
                        var req = reply.outgoing[i];
                        string rname = string.IsNullOrEmpty(req.name) ? CloudFormat.ShortKey(req.key) : req.name;
                        string rkey = req.key;
                        long rid = req.id;
                        var row = Box(left.transform, 20, yCursor, 680, 62, new Color(.05f, .13f, .21f, .92f));
                        AvatarPortrait(row.transform, null, rname, 10, 7, 48);
                        Label(row.transform, "@" + rname, 68, 8, 390, 24, 17, White, true);
                        Label(row.transform, "Ожидает подтверждения…", 68, 33, 390, 20, 13, Muted);
                        Button("Отменить", 536, 9, 132, 44, () =>
                        {
                            if (!string.IsNullOrEmpty(rkey)) a.CloudFriendRemove(rkey, AfterCloud);
                            else a.CloudFriendDecline(rid, AfterCloud);
                        }, false, row.transform);
                        yCursor += 68;
                    }
                }
            }

            // 3. Right Panel: Inspect Selected Friend Profile Card OR Add Friend + Quick Requests
            var right = Box(content.transform, 808, 212, 728, 568, Panel);
            if (!string.IsNullOrEmpty(selectedFriendName))
            {
                AccountCard card = null;
                a.Cards.TryGetValue(selectedFriendName, out card);
                string fav = card != null ? card.avatar : null;

                Label(right.transform, "ПРОФИЛЬ ДРУГА", 24, 18, 380, 24, 14, Accent, true);
                Button("← Добавить друга", 500, 14, 204, 38, () => { selectedFriendName = ""; selectedFriendKey = ""; confirmRemoveFriend = false; ShowFriends(); }, false, right.transform);

                AvatarPortrait(right.transform, fav, selectedFriendName, 24, 58, 104);
                var dot = Box(right.transform, 112, 146, 16, 16, selectedFriendOnline ? SpringAccent : new Color(.45f, .55f, .65f, .9f));
                dot.raycastTarget = false;

                Label(right.transform, "@" + selectedFriendName, 146, 56, 360, 36, 26, White, true);
                Button("Копировать @ник", 520, 58, 184, 38, () =>
                {
                    GUIUtility.systemCopyBuffer = selectedFriendName;
                    cloudNotice = "Ник @" + selectedFriendName + " скопирован!";
                    ShowFriends();
                }, false, right.transform);

                string statusText = selectedFriendOnline ? "● Сейчас в игре" : "○ Не в сети";
                string roleText = card != null ? ("  ·  Уровень " + card.level + "  ·  " + card.titleName) : "  ·  Загружаем карточку героя…";
                Label(right.transform, statusText + roleText, 146, 96, 550, 24, 15, selectedFriendOnline ? SpringAccent : Accent);

                string motto = card != null && !string.IsNullOrEmpty(card.bio)
                    ? "«" + card.bio + "»"
                    : "Путешественник солнечной Лестингии и парящих островов.";
                Label(right.transform, motto, 146, 126, 554, 42, 14, Muted);

                if (card != null)
                {
                    int nextXp = AccountClient.XpFloorForLevel(card.level + 1);
                    float ratio = AccountClient.XpProgress01(card.xp, card.level);
                    Label(right.transform, "Опыт экспедиций: " + card.xp + " / " + nextXp + " XP   ·   Достижений: " + card.achievementCount + " / 200", 24, 184, 680, 22, 14, White, true);
                    var bar = Box(right.transform, 24, 210, 680, 12, new Color(.04f, .10f, .16f, .95f));
                    if (ratio > 0f) Box(bar.transform, 0, 0, Mathf.Max(10f, 680f * ratio), 12, Accent);

                    string[] labels = { "ДОБЫТО БЛОКОВ", "ПОСТРОЕНО", "ОХОТА", "В МИРЕ" };
                    string[] vals = {
                        CloudFormat.Coins(StatOf(card, "mined")),
                        CloudFormat.Coins(StatOf(card, "placed")),
                        CloudFormat.Coins(StatOf(card, "kills")),
                        (StatOf(card, "alive_s") / 60) + " мин"
                    };
                    for (int s = 0; s < 4; s++)
                    {
                        float tx = 24 + (s % 2) * 346, ty = 242 + (s / 2) * 92;
                        var tile = Box(right.transform, tx, ty, 334, 80, new Color(.05f, .13f, .21f, .92f));
                        Label(tile.transform, labels[s], 16, 10, 300, 18, 11, Accent, true);
                        Label(tile.transform, vals[s], 16, 32, 300, 34, 22, White, true);
                    }
                }
                else
                {
                    var loadingTile = Box(right.transform, 24, 200, 680, 180, new Color(.05f, .13f, .21f, .90f));
                    Label(loadingTile.transform, "Загружаем достижения и статистику @" + selectedFriendName + "…", 24, 70, 632, 40, 16, Muted, false, TextAnchor.MiddleCenter);
                }

                Button("Пригласить в кооператив  →", 24, 444, 420, 52, () => InviteFriendToCoop(selectedFriendName, openRooms: true), true, right.transform);
                string removeLabel = confirmRemoveFriend ? "Подтвердить удаление?" : "Удалить из друзей";
                Button(removeLabel, 458, 444, 246, 52, () =>
                {
                    if (!confirmRemoveFriend) { confirmRemoveFriend = true; cloudNotice = "Нажмите ещё раз, чтобы удалить @" + selectedFriendName + " из списка друзей."; ShowFriends(); return; }
                    string keyToRemove = selectedFriendKey;
                    selectedFriendName = ""; selectedFriendKey = ""; confirmRemoveFriend = false;
                    if (!string.IsNullOrEmpty(keyToRemove)) a.CloudFriendRemove(keyToRemove, AfterCloud);
                }, false, right.transform);
            }
            else
            {
                // Default Right Mode: Add Friend by @Nickname + Greeting + Quick Incoming Requests!
                Label(right.transform, "ДОБАВИТЬ ДРУГА ПО @НИКУ", 24, 18, 660, 26, 16, Accent, true);
                Label(right.transform, "Введите @ник друга (можно с символом @ или без него) и короткое приветствие.", 24, 46, 680, 24, 13, Muted);

                InputIn(right.transform, "НИК ИГРОКА · 3–24 латинских символа", friendKey, "например @ehot_night", 24, 80, 680, v => friendKey = v, 26);
                InputIn(right.transform, "ПРИВЕТСТВИЕ К ЗАЯВКЕ (необязательно)", friendGreeting, "Пошли строить замок на парящем острове!", 24, 166, 680, v => friendGreeting = v, 80);

                Button("«Пошли в кооп!»", 24, 250, 186, 36, () => { friendGreeting = "Привет! Пошли вместе в кооператив!"; ShowFriends(); }, false, right.transform);
                Button("«Строим на островах!»", 220, 250, 224, 36, () => { friendGreeting = "Пошли строить базу на парящем острове!"; ShowFriends(); }, false, right.transform);

                var sendBtn = Button("Отправить заявку в друзья  →", 24, 298, 680, 54, () =>
                {
                    string nick = (friendKey ?? "").Trim().TrimStart('@');
                    if (!Regex.IsMatch(nick, "^[A-Za-z0-9_]{3,24}$"))
                    {
                        cloudNotice = "Ник должен содержать 3–24 латинские буквы, цифры или _.";
                        ShowFriends();
                        return;
                    }
                    cloudNotice = "Отправляем заявку игроку @" + nick + "…";
                    a.CloudFriendRequestByName(nick, () =>
                    {
                        if (string.IsNullOrEmpty(a.CloudMessage))
                            cloudNotice = "Заявка в друзья для @" + nick + " успешно отправлена!";
                        else
                            cloudNotice = "";
                        AfterCloud();
                    }, friendGreeting);
                    friendKey = "";
                }, true, right.transform);
                sendBtn.name = "Friends.SendRequest";

                Box(right.transform, 24, 368, 680, 1, new Color(1f, 1f, 1f, .14f), false);
                Label(right.transform, "БЫСТРЫЕ ВХОДЯЩИЕ ЗАЯВКИ (" + incCount + ")", 24, 382, 680, 22, 13, Accent, true);
                if (incCount == 0)
                {
                    Label(right.transform, "Пока нет новых заявок. Поделитесь своим ником @" + myNick + " с друзьями!", 24, 412, 680, 44, 14, Muted);
                }
                else
                {
                    for (int i = 0; i < Mathf.Min(incCount, 2); i++)
                    {
                        var req = reply.incoming[i];
                        long id = req.id;
                        string rname = string.IsNullOrEmpty(req.name) ? "player" : req.name;
                        var row = Box(right.transform, 24, 412 + i * 72, 680, 64, new Color(.05f, .13f, .21f, .92f));
                        AvatarPortrait(row.transform, null, rname, 10, 8, 48);
                        Label(row.transform, "@" + rname + " хочет в друзья", 68, 8, 380, 24, 16, White, true);
                        Label(row.transform, string.IsNullOrEmpty(req.message) ? "Без сообщения" : req.message, 68, 34, 380, 22, 12, Muted);
                        Button("Принять", 462, 10, 106, 44, () => a.CloudFriendAccept(id, AfterCloud), true, row.transform);
                        Button("Нет", 576, 10, 92, 44, () => a.CloudFriendDecline(id, AfterCloud), false, row.transform);
                    }
                }
            }

            // 4. Bottom Navigation Bar
            var backButton = Button(game && game.InWorld ? "← В паузу" : "← В главное меню", 64, 804, 280, 52, () => { cloudNotice = ""; if (game && game.InWorld) ShowPause(); else ShowHome(); });
            backButton.name = "Friends.Back";
            Button("Мой профиль", 360, 804, 240, 52, () => { cloudNotice = ""; ShowAccount(); });
            Button("Кошелёк и скины  →", 616, 804, 300, 52, () => { cloudNotice = ""; ShowStore(); });
            Button("Мультиплеер  →", 932, 804, 260, 52, () => { cloudNotice = ""; ShowRooms(); }, true);

            string bottomNote = !string.IsNullOrEmpty(cloudNotice) ? cloudNotice : (!string.IsNullOrEmpty(a.CloudMessage) ? a.CloudMessage : "");
            status = Label(content.transform, bottomNote, 1204, 800, 332, 58, 13, Accent, false, TextAnchor.MiddleRight);
        }

        void InspectFriend(CloudFriend friend)
        {
            if (friend == null) return;
            selectedFriendName = string.IsNullOrEmpty(friend.name) ? "" : friend.name;
            selectedFriendKey = friend.key ?? "";
            selectedFriendOnline = friend.online;
            confirmRemoveFriend = false;
            cloudNotice = "";
            ShowFriends();
            var a = Accounts;
            if (a != null && a.Logged && !string.IsNullOrEmpty(selectedFriendName))
                a.LookupCard(selectedFriendName, (card, err) =>
                {
                    if (!string.IsNullOrEmpty(err)) cloudNotice = err;
                    if (page == Page.Friends) ShowFriends();
                });
        }

        void InviteFriendToCoop(string friendUsername, bool openRooms = false)
        {
            string addr = game != null ? game.EndpointAddress : "";
            string myNick = Accounts != null && Accounts.Logged && Accounts.Profile != null ? Accounts.Profile.username : ProfileAvatar.LocalNickname;
            string invite = "Заходи ко мне в LestingCraft: New Era! Мой ник: @" + myNick + (!string.IsNullOrEmpty(addr) && addr != "адрес не настроен" ? " · Сервер: " + addr : "");
            GUIUtility.systemCopyBuffer = invite;
            cloudNotice = "Приглашение для @" + friendUsername + " скопировано в буфер обмена!";
            if (openRooms && (game == null || !game.InWorld)) ShowRooms();
            else ShowFriends();
        }

        static int StatOf(AccountCard card, string key)
        {
            if (card == null || card.stats == null) return 0;
            foreach (var s in card.stats)
                if (s != null && s.key == key) return s.value;
            return 0;
        }
    }
}
