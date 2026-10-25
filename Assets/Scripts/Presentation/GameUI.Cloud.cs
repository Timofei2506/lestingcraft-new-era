using System;
using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Gameplay;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Wallet, cosmetics and friends. Everything here is served by the game
    /// server from Supabase: no Supabase URL or key is ever shipped to a player.
    /// </summary>
    public sealed partial class GameUI
    {
        string cloudNotice = "";
        string marketCode = "";
        string marketPrice = "";
        string friendKey = "";

        AccountClient Accounts { get { return game.Accounts; } }

        /// <summary>Re-renders whatever cloud screen is open after a request.</summary>
        void AfterCloud()
        {
            if (page == Page.Store) ShowStore();
            else if (page == Page.Market) ShowMarket();
            else if (page == Page.Friends) ShowFriends();
        }

        void RequireLogin(Page target,string what)
        {
            Clear(target);
            Header(what);
            Label(content.transform, "НУЖЕН АККАУНТ", 64, 200, 1200, 56, 34, White, true);
            Label(content.transform, "Кошелёк, скины и друзья живут в аккаунте: он один на все устройства и на сайт.\nВойдите — и монеты, покупки и список друзей появятся здесь.",
                66, 270, 1200, 110, 19, Muted);
            Button("Войти или зарегистрироваться  →", 64, 400, 520, 64, ShowAccount, true);
            Button("← В главное меню", 604, 400, 380, 64, ShowHome);
        }

        public void ShowStore()
        {
            Clear(Page.Store);
            Header("Кошелёк и скины");
            var a = Accounts;
            if (!a.Logged) { RequireLogin(Page.Store, "Кошелёк и скины"); return; }
            if (a.Wallet == null) { a.CloudState(AfterCloud); }

            var wallet = a.Wallet;
            long balance = wallet != null ? wallet.balance : 0;
            var left = Box(content.transform, 64, 150, 470, 640, Panel);
            Label(left.transform, "КОШЕЛЁК", 26, 20, 400, 30, 17, Accent, true);
            Label(left.transform, CloudFormat.Coins(balance), 24, 54, 420, 62, 46, White, true);
            Label(left.transform, "монет", 28, 112, 300, 24, 15, Muted);
            Box(left.transform, 26, 150, 418, 1, new Color(.8f, .9f, .8f, .16f), false);

            Label(left.transform, "ПОПОЛНЕНИЕ", 26, 168, 400, 24, 13, Accent, true);
            if (wallet == null || wallet.packs == null || wallet.packs.Length == 0)
                Label(left.transform, "Паки за реальные деньги появятся, когда\nподключим платёжный провайдер.\nСтартовых монет нет: заработать их можно\nтолько продажей на маркетплейсе.",
                    26, 198, 420, 110, 15, Muted);
            else
                for (int i = 0; i < Mathf.Min(wallet.packs.Length, 4); i++)
                {
                    var pack = wallet.packs[i];
                    var row = Button("", 22, 196 + i * 62, 426, 54, () => { }, false, left.transform);
                    Label(row.transform, pack.title, 16, 6, 240, 24, 17, White, true);
                    Label(row.transform, pack.CoinsText + " монет", 16, 30, 240, 20, 13, Muted);
                    Label(row.transform, pack.PriceText, 268, 14, 140, 24, 16, Accent, false, TextAnchor.MiddleRight);
                }

            Label(left.transform, "ПОСЛЕДНИЕ ОПЕРАЦИИ", 26, 456, 400, 24, 13, Accent, true);
            if (wallet == null || wallet.ledger == null || wallet.ledger.Length == 0)
                Label(left.transform, "Пока пусто.", 26, 486, 400, 24, 15, Muted);
            else
                for (int i = 0; i < Mathf.Min(wallet.ledger.Length, 5); i++)
                {
                    var row = wallet.ledger[i];
                    Label(left.transform, CloudFormat.Reason(row.reason) + "   " + (row.delta > 0 ? "+" : "") + CloudFormat.Coins(row.delta),
                        26, 486 + i * 28, 418, 24, 15, row.delta > 0 ? White : Muted);
                }

            var right = Box(content.transform, 562, 150, 974, 640, Panel);
            Label(right.transform, "МАГАЗИН СКИНОВ", 28, 20, 600, 30, 17, Accent, true);
            Label(right.transform, "Купленный скин можно надеть на персонажа — он виден другим игрокам.", 28, 50, 900, 24, 14, Muted);
            var owned = wallet != null ? wallet.owned : null;
            var store = wallet != null ? wallet.store : null;
            if (store == null || store.Length == 0)
                Label(right.transform, "Магазин загружается…", 28, 96, 700, 30, 16, Muted);
            for (int i = 0; store != null && i < Mathf.Min(store.Length, 6); i++)
            {
                var item = store[i];
                bool has = false;
                if (owned != null) foreach (var mine in owned) if (mine.code == item.code) has = true;
                bool worn = wallet != null && wallet.profile != null && wallet.profile.avatar_skin == item.code;
                var row = Box(right.transform, 24, 92 + i * 86, 926, 76, new Color(.05f, .08f, .09f, .98f));
                Label(row.transform, item.title, 20, 10, 460, 26, 19, White, true);
                Label(row.transform, CloudFormat.Kind(item.kind) + "  ·  " + (item.description ?? ""), 20, 40, 470, 26, 14, Muted);
                string action = worn ? "Надето" : has ? "Надеть" : CloudFormat.Coins(item.price) + " монет";
                var button = Button(action, 640, 12, 262, 52, () =>
                {
                    if (worn) return;
                    if (has) a.CloudEquip(item.code, AfterCloud);
                    else a.CloudBuy(item.code, AfterCloud);
                }, !worn && !has, row.transform);
                button.name = "Store." + item.code;
            }

            Button("Маркетплейс игроков  →", 64, 806, 420, 56, () => { a.CloudMarket(AfterCloud); ShowMarket(); }, true);
            Button("Друзья  →", 500, 806, 300, 56, () => { a.CloudFriends(AfterCloud); ShowFriends(); });
            Button("← Назад", 1260, 806, 276, 56, () => { cloudNotice = ""; ShowAccount(); });
            status = Label(content.transform, !string.IsNullOrEmpty(cloudNotice) ? cloudNotice : string.IsNullOrEmpty(a.CloudMessage) ? "Монеты покупаются за реальные деньги; стартовых начислений нет." : a.CloudMessage,
                64, 746, 1000, 40, 15, Accent);
        }

        public void ShowMarket()
        {
            Clear(Page.Market);
            Header("Маркетплейс");
            var a = Accounts;
            if (!a.Logged) { RequireLogin(Page.Market, "Маркетплейс"); return; }
            if (a.Board == null) a.CloudMarket(AfterCloud);
            var board = a.Board;
            var wallet = a.Wallet;

            var left = Box(content.transform, 64, 150, 940, 520, Panel);
            Label(left.transform, "ЛОТЫ ИГРОКОВ", 26, 18, 600, 30, 17, Accent, true);
            Label(left.transform, "Комиссия 5% платит продавец. Предмет уходит в эскроу, пока лот активен.", 26, 48, 880, 24, 14, Muted);
            var listings = board != null ? board.listings : null;
            if (listings == null || listings.Length == 0)
                Label(left.transform, "Активных лотов нет. Свой скин можно выставить справа.", 26, 96, 800, 30, 16, Muted);
            for (int i = 0; listings != null && i < Mathf.Min(listings.Length, 5); i++)
            {
                var lot = listings[i];
                var row = Box(left.transform, 22, 88 + i * 76, 896, 66, new Color(.05f, .08f, .09f, .98f));
                Label(row.transform, lot.title ?? lot.item, 18, 8, 420, 26, 18, White, true);
                Label(row.transform, (lot.seller ?? "Игрок") + "  ·  " + CloudFormat.Kind(lot.kind), 18, 36, 420, 22, 14, Muted);
                Label(row.transform, CloudFormat.Coins(lot.price) + " монет", 470, 20, 200, 26, 17, Accent, false, TextAnchor.MiddleRight);
                if (lot.mine)
                    Button("Снять", 690, 8, 186, 50, () => a.CloudCancel(lot.id, AfterCloud), false, row.transform);
                else
                {
                    var buy = Button("Купить", 690, 8, 186, 50, () => a.CloudPurchase(lot.id, AfterCloud), true, row.transform);
                    buy.name = "Market.buy." + lot.id;
                }
            }

            var right = Box(content.transform, 1028, 150, 508, 520, Panel);
            Label(right.transform, "ПРОДАТЬ СВОЙ СКИН", 24, 18, 460, 30, 17, Accent, true);
            var owned = wallet != null ? wallet.owned : null;
            if (owned == null || owned.Length == 0)
                Label(right.transform, "У вас нет свободных скинов.\nКупленный и выставленный скин\nнельзя продать дважды.", 24, 60, 460, 90, 15, Muted);
            else
                for (int i = 0; i < Mathf.Min(owned.Length, 4); i++)
                {
                    var item = owned[i];
                    bool chosen = marketCode == item.code;
                    var row = Button("", 20, 60 + i * 58, 468, 50, () => { marketCode = item.code; ShowMarket(); }, chosen, right.transform);
                    Label(row.transform, item.title, 16, 6, 430, 22, 16, chosen ? Ink : White, true);
                    Label(row.transform, CloudFormat.Kind(item.kind), 16, 27, 300, 18, 12, chosen ? Ink : Muted);
                }
            Input("ЦЕНА В МОНЕТАХ", marketPrice, "Например 250", 1028 + 24, 358, 200, v => marketPrice = v);
            var listButton = Button("Выставить лот", 1258, 392, 250, 54, () =>
            {
                long price;
                if (string.IsNullOrEmpty(marketCode)) { cloudNotice = "Сначала выберите скин слева."; ShowMarket(); return; }
                if (!long.TryParse((marketPrice ?? "").Trim(), out price) || price <= 0) { cloudNotice = "Цена должна быть целым числом больше нуля."; ShowMarket(); return; }
                cloudNotice = "";
                a.CloudList(marketCode, price, AfterCloud);
                marketCode = ""; marketPrice = "";
            }, true, right.transform);
            listButton.name = "Market.list";

            if (wallet != null && wallet.listings != null && wallet.listings.Length > 0)
                Label(right.transform, "Ваши лоты: " + wallet.listings.Length, 24, 460, 460, 26, 15, Accent);

            Button("← Кошелёк и скины", 64, 806, 420, 56, () => { cloudNotice = ""; ShowStore(); });
            Button("Обновить", 500, 806, 260, 56, () => a.CloudMarket(AfterCloud));
            Button("← Назад", 1260, 806, 276, 56, () => { cloudNotice = ""; ShowAccount(); });
            status = Label(content.transform, !string.IsNullOrEmpty(cloudNotice) ? cloudNotice : string.IsNullOrEmpty(a.CloudMessage) ? "Покупка проверяется на сервере: баланс и эскроу меняются атомарно." : a.CloudMessage,
                64, 700, 1400, 40, 15, Accent);
        }

        public void ShowFriends()
        {
            Clear(Page.Friends);
            Header("Друзья");
            var a = Accounts;
            if (!a.Logged) { RequireLogin(Page.Friends, "Друзья"); return; }
            if (a.Friends == null) a.CloudFriends(AfterCloud);
            var friends = a.Friends;

            var left = Box(content.transform, 64, 150, 700, 560, Panel);
            Label(left.transform, "МОИ ДРУЗЬЯ", 26, 18, 500, 30, 17, Accent, true);
            if (friends == null)
                Label(left.transform, "Загружаю список…", 26, 70, 600, 30, 16, Muted);
            else if (friends.friends == null || friends.friends.Length == 0)
                Label(left.transform, "Пока никого. Добавьте друга по ключу игрока:\nсвой ключ виден на экране «Аккаунт», кнопка «Скопировать мой ключ».", 26, 66, 640, 90, 16, Muted);
            else
                for (int i = 0; i < Mathf.Min(friends.friends.Length, 7); i++)
                {
                    var friend = friends.friends[i];
                    var row = Box(left.transform, 22, 62 + i * 66, 656, 58, new Color(.05f, .08f, .09f, .98f));
                    Label(row.transform, string.IsNullOrEmpty(friend.name) ? CloudFormat.ShortKey(friend.key) : friend.name, 18, 8, 380, 26, 18, White, true);
                    Label(row.transform, friend.online ? "в игре" : "не в сети", 18, 32, 380, 20, 13, friend.online ? Accent : Muted);
                    Button("Удалить", 470, 6, 176, 46, () => a.CloudFriendRemove(friend.key, AfterCloud), false, row.transform);
                }

            var right = Box(content.transform, 788, 150, 748, 560, Panel);
            Label(right.transform, "ЗАЯВКИ", 26, 18, 500, 30, 17, Accent, true);
            int shown = 0;
            if (friends != null && friends.incoming != null)
                foreach (var request in friends.incoming)
                {
                    if (shown >= 3) break;
                    long id = request.id;
                    var row = Box(right.transform, 22, 62 + shown * 74, 704, 66, new Color(.05f, .08f, .09f, .98f));
                    Label(row.transform, (string.IsNullOrEmpty(request.name) ? CloudFormat.ShortKey(request.key) : request.name) + " хочет в друзья", 18, 8, 460, 26, 17, White, true);
                    Label(row.transform, string.IsNullOrEmpty(request.message) ? "без сообщения" : request.message, 18, 34, 460, 22, 13, Muted);
                    Button("Принять", 500, 8, 94, 50, () => a.CloudFriendAccept(id, AfterCloud), true, row.transform);
                    Button("Нет", 604, 8, 90, 50, () => a.CloudFriendDecline(id, AfterCloud), false, row.transform);
                    shown++;
                }
            if (shown == 0) Label(right.transform, "Новых заявок нет.", 26, 66, 600, 26, 15, Muted);

            Label(right.transform, "ДОБАВИТЬ ПО КЛЮЧУ ИГРОКА", 26, 300, 700, 26, 14, Accent, true);
            Input("КЛЮЧ · 32 символа", friendKey, "например 4f2c90fa05094bdc887c19e7f7c69a85", 812, 340, 560, v => friendKey = v);
            Button("Отправить заявку", 812, 434, 300, 54, () =>
            {
                string key = (friendKey ?? "").Trim().ToLowerInvariant();
                if (key.Length != 32) { cloudNotice = "Ключ игрока — 32 символа латиницей и цифрами."; ShowFriends(); return; }
                cloudNotice = "";
                a.CloudFriendRequest(key, AfterCloud);
                friendKey = "";
            }, true);
            Label(right.transform, "Заявка не выдаёт, существует ли такой игрок: ответ всегда один и тот же.", 812, 498, 700, 40, 13, Muted);

            Button("← Кошелёк и скины", 64, 806, 420, 56, () => { cloudNotice = ""; ShowStore(); });
            Button("Обновить", 500, 806, 260, 56, () => a.CloudFriends(AfterCloud));
            Button("← Назад", 1260, 806, 276, 56, () => { cloudNotice = ""; ShowAccount(); });
            status = Label(content.transform, !string.IsNullOrEmpty(cloudNotice) ? cloudNotice : string.IsNullOrEmpty(a.CloudMessage) ? "Друзья видны на всех устройствах и на сайте." : a.CloudMessage,
                64, 730, 1400, 40, 15, Accent);
        }
    }
}
