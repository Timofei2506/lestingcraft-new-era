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
            Button("Маркетплейс игроков  →", 330, 804, 360, 52, () => { a.CloudMarket(AfterCloud); ShowMarket(); }, true);
            Button("Друзья  →", 706, 804, 240, 52, () => { a.CloudFriends(AfterCloud); ShowFriends(); });
            Button("Мой профиль", 962, 804, 240, 52, () => { cloudNotice = ""; ShowAccount(); });
            status = Label(content.transform, !string.IsNullOrEmpty(cloudNotice) ? cloudNotice : string.IsNullOrEmpty(a.CloudMessage) ? "" : a.CloudMessage,
                1216, 804, 320, 52, 13, Accent, false, TextAnchor.MiddleRight);
        }

        public void ShowMarket()
        {
            Clear(Page.Market);
            Header("Маркетплейс", false);
            var a = Accounts;
            if (a == null || !a.Logged) { RequireLogin(Page.Market, "Маркетплейс"); return; }
            if (a.Board == null && !a.CloudBusy) a.CloudMarket(AfterCloud);
            var board = a.Board;
            var wallet = a.Wallet;

            var left = Box(content.transform, 64, 150, 940, 540, Panel);
            Label(left.transform, "ЛОТЫ ИГРОКОВ", 26, 18, 600, 30, 17, Accent, true);
            Label(left.transform, "Комиссия 5% платит продавец. Предмет находится в защищённом эскроу, пока лот активен.", 26, 48, 880, 24, 14, Muted);
            var listings = board != null ? board.listings : null;
            if (listings == null || listings.Length == 0)
                Label(left.transform, "Активных лотов пока нет. Выставьте свой свободный скин справа!", 26, 96, 800, 30, 16, Muted);
            for (int i = 0; listings != null && i < Mathf.Min(listings.Length, 5); i++)
            {
                var lot = listings[i];
                var row = Box(left.transform, 22, 88 + i * 78, 896, 68, new Color(.05f, .13f, .21f, .92f));
                Label(row.transform, lot.title ?? lot.item, 18, 8, 420, 26, 18, White, true);
                Label(row.transform, (lot.seller ?? "Игрок") + "  ·  " + CloudFormat.Kind(lot.kind), 18, 36, 420, 22, 14, Muted);
                Label(row.transform, CloudFormat.Coins(lot.price) + " монет", 470, 20, 200, 26, 17, Accent, false, TextAnchor.MiddleRight);
                if (lot.mine)
                    Button("Снять", 690, 9, 186, 50, () => a.CloudCancel(lot.id, AfterCloud), false, row.transform);
                else
                {
                    var buy = Button("Купить", 690, 9, 186, 50, () => a.CloudPurchase(lot.id, AfterCloud), true, row.transform);
                    buy.name = "Market.buy." + lot.id;
                }
            }

            var right = Box(content.transform, 1028, 150, 508, 540, Panel);
            Label(right.transform, "ПРОДАТЬ СВОЙ СКИН", 24, 18, 460, 30, 17, Accent, true);
            var owned = wallet != null ? wallet.owned : null;
            if (owned == null || owned.Length == 0)
                Label(right.transform, "У вас нет свободных скинов.\nНадетый или выставленный скин\nнельзя продать повторно.", 24, 60, 460, 90, 15, Muted);
            else
                for (int i = 0; i < Mathf.Min(owned.Length, 4); i++)
                {
                    var item = owned[i];
                    bool chosen = marketCode == item.code;
                    var row = Button("", 20, 60 + i * 58, 468, 50, () => { marketCode = item.code; ShowMarket(); }, chosen, right.transform);
                    Label(row.transform, item.title, 16, 6, 430, 22, 16, chosen ? Ink : White, true);
                    Label(row.transform, CloudFormat.Kind(item.kind), 16, 27, 300, 18, 12, chosen ? Ink : Muted);
                }
            InputIn(right.transform, "ЦЕНА В МОНЕТАХ", marketPrice, "Например 250", 24, 304, 210, v => marketPrice = v, 10);
            var listButton = Button("Выставить лот", 248, 328, 236, 52, () =>
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
                Label(right.transform, "Активных ваших лотов: " + wallet.listings.Length, 24, 412, 460, 26, 15, Accent);

            Button("← Кошелёк и скины", 64, 804, 340, 52, () => { cloudNotice = ""; ShowStore(); });
            Button("Обновить", 420, 804, 220, 52, () => a.CloudMarket(AfterCloud));
            Button("← В главное меню", 1260, 804, 276, 52, () => { cloudNotice = ""; if (game && game.InWorld) ShowPause(); else ShowHome(); });
            status = Label(content.transform, !string.IsNullOrEmpty(cloudNotice) ? cloudNotice : string.IsNullOrEmpty(a.CloudMessage) ? "Покупка проверяется на сервере атомарно." : a.CloudMessage,
                64, 718, 1472, 36, 15, Accent);
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
