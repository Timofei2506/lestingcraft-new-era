using System;
using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Gameplay;
using LestingCraft.Networking;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Minecraft style multiplayer screens: a saved list of server addresses
    /// ("Добавить сервер" with IP and port), the worlds published on the chosen
    /// address and a one-button "Создать сервер" for the world being played.
    /// </summary>
    public sealed partial class GameUI
    {
        int serverSelection = -1;
        PublicServer[] serverWorlds;
        string serverMessage = "";
        bool serverLoading;
        string addAddress = "", addPort = "443", addName = "";

        public void ShowRooms()
        {
            Clear(Page.Rooms);
            Header("Мультиплеер");
            Label(content.transform, "СЕРВЕРА", 64, 150, 700, 44, 32, White, true);
            Label(content.transform, "Добавь адрес сервера и зайди в опубликованный там мир — как в Minecraft.", 66, 196, 1100, 30, 16, Muted);
            var book = game.Servers;

            var left = Box(content.transform, 64, 236, 760, 470, Panel);
            Label(left.transform, "МОИ АДРЕСА", 28, 20, 500, 30, 18, Accent, true);
            Label(left.transform, book.Count + "/" + ServerBook.Capacity, 560, 20, 172, 30, 15, Muted, false, TextAnchor.MiddleRight);
            if (book.Count == 0)
                Label(left.transform, "Пока пусто.\nНажми «Добавить сервер» и введи IP и порт —\nнапример 192.168.1.7 и 8787.", 28, 74, 700, 110, 17, White);
            for (int i = 0; i < Mathf.Min(book.Count, 5); i++)
            {
                int index = i;
                var entry = book.Servers[i];
                bool chosen = serverSelection == index;
                var row = Button("", 24, 70 + i * 74, 712, 64, () => SelectServer(index), chosen, left.transform);
                Label(row.transform, entry.Title, 18, 8, 400, 26, 19, chosen ? Ink : White, true);
                Label(row.transform, entry.Address, 18, 34, 400, 22, 14, chosen ? Ink : Muted);
                string population = entry.lastPlayers < 0 ? "не проверен" : entry.lastPlayers + " / 8";
                Label(row.transform, population, 500, 18, 176, 26, 15, chosen ? Ink : Accent, false, TextAnchor.MiddleRight);
            }

            var right = Box(content.transform, 852, 236, 684, 470, Panel);
            if (serverSelection < 0 || serverSelection >= book.Count)
            {
                Label(right.transform, "ВЫБЕРИ СЕРВЕР", 28, 20, 600, 30, 18, Accent, true);
                Label(right.transform, "Слева — адреса, которые ты добавил.\nНажми на сервер, чтобы увидеть его миры.", 28, 74, 620, 90, 17, White);
            }
            else
            {
                var entry = book.Servers[serverSelection];
                Label(right.transform, entry.Title.ToUpperInvariant(), 28, 16, 420, 32, 22, White, true);
                Label(right.transform, entry.Address + "   ·   " + (serverLoading ? "проверяю…" : (serverWorlds == null ? "нет данных" : serverWorlds.Length + " мир(ов)")),
                    28, 52, 460, 24, 14, Muted);
                var refresh = Button("Обновить", 520, 18, 136, 46, RefreshSelectedServer, false, right.transform);
                refresh.name = "Rooms.Refresh";
                for (int i = 0; i < (serverWorlds == null ? 0 : Mathf.Min(serverWorlds.Length, 5)); i++)
                {
                    var world = serverWorlds[i];
                    string title = string.IsNullOrEmpty(world.name) ? "Мир" : world.name;
                    var row = Button("", 24, 84 + i * 70, 636, 62, () => JoinWorld(entry, world), false, right.transform);
                    Label(row.transform, title, 18, 8, 380, 26, 18, White, true);
                    Label(row.transform, GameRules.ModeNames[Mathf.Clamp(world.mode, 0, 3)] + "  ·  " + world.PopulationText, 18, 34, 380, 22, 14, Muted);
                    Label(row.transform, "Войти  →", 470, 18, 146, 26, 16, Accent, false, TextAnchor.MiddleRight);
                }
                if (!serverLoading && serverWorlds != null && serverWorlds.Length == 0)
                    Label(right.transform, "На этом сервере пока нет опубликованных миров.\nИх создаёт кнопка «Создать сервер» в паузе игры.", 28, 96, 620, 90, 16, White);
            }
            if (!string.IsNullOrEmpty(serverMessage))
                Label(content.transform, serverMessage, 66, 716, 1470, 30, 15, Accent);

            var add = Button("Добавить сервер", 64, 760, 320, 60, ShowAddServer);
            add.name = "Rooms.Add";
            var host = Button("Создать свой сервер", 400, 760, 360, 60, HostFromMenu, true);
            host.name = "Rooms.Host";
            var back = Button("← В главное меню", 1256, 760, 280, 60, ShowHome);
            back.name = "Rooms.Back";
            status = Label(content.transform, "Серверы ищутся по адресу. Порт по умолчанию — 443 (HTTPS).", 64, 828, 1000, 28, 14, Muted);
        }

        void SelectServer(int index)
        {
            if (index < 0 || index >= game.Servers.Count) return;
            serverSelection = index;
            serverWorlds = null;
            serverMessage = "";
            serverLoading = true;
            ShowRooms();
            var entry = game.Servers.Servers[index];
            game.BrowseServers(entry,
                worlds =>
                {
                    serverLoading = false;
                    if (page != Page.Rooms) return;
                    serverWorlds = worlds ?? new PublicServer[0];
                    int people = 0;
                    foreach (var world in serverWorlds) people += Mathf.Max(0, world.players);
                    game.Servers.MarkSeen(index, people, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    game.SaveServers();
                    ShowRooms();
                },
                error =>
                {
                    serverLoading = false;
                    if (page != Page.Rooms) return;
                    serverWorlds = new PublicServer[0];
                    serverMessage = "Сервер не ответил: " + error;
                    ShowRooms();
                });
        }

        void RefreshSelectedServer()
        {
            if (serverSelection < 0 || serverSelection >= game.Servers.Count) return;
            SelectServer(serverSelection);
        }

        void JoinWorld(ServerAddress entry, PublicServer world)
        {
            if (world == null || string.IsNullOrEmpty(world.code)) return;
            serverMessage = "";
            game.JoinPublishedWorld(entry.Url, world.code, world.name);
        }

        void HostFromMenu()
        {
            ShowAddServer(true);
        }

        public void ShowAddServer()
        {
            ShowAddServer(false);
        }

        void ShowAddServer(bool hosting)
        {
            Clear(Page.AddServer);
            Header(hosting ? "Создать свой сервер" : "Добавить сервер");
            Label(content.transform, hosting ? "СОЗДАТЬ СВОЙ СЕРВЕР" : "НОВЫЙ АДРЕС", 64, 150, 900, 44, 32, White, true);
            Label(content.transform, hosting
                ? "Мир создастся на сервере " + game.EndpointAddress + ", и ты сразу в него зайдёшь. Друзьям достаточно добавить этот адрес."
                : "Введи IP или домен и порт. Если сервер работает на 443 (HTTPS), порт можно оставить по умолчанию.",
                66, 196, 1400, 56, 16, Muted);

            var card = Box(content.transform, 380, 274, 840, 372, Panel);
            Label(card.transform, hosting ? "ИМЯ МИРА" : "АДРЕС И ПОРТ", 40, 26, 700, 28, 17, Accent, true);
            if (hosting)
            {
                Input("НАЗВАНИЕ МИРА", addName, "Тихий Брод", 420, 330, 760, v => addName = v);
                Label(content.transform, "Мир начнётся в Лестингии. Режим: " + GameRules.ModeNames[game.SelectedMode] + ".", 422, 452, 760, 26, 14, Muted);
                var create = Button("Создать сервер и войти  →", 420, 520, 470, 64, SubmitHost, true);
                create.name = "AddServer.Create";
                Button("← Назад", 906, 520, 274, 64, ShowRooms);
                status = Label(content.transform, serverMessage, 420, 604, 760, 30, 14, Accent);
                return;
            }

            Input("АДРЕС СЕРВЕРА", addAddress, "example.com или 192.168.1.7:8787", 420, 330, 560, v => addAddress = v);
            Input("ПОРТ", addPort, "443", 1010, 330, 170, v => addPort = v);
            Input("НАЗВАНИЕ", addName, "Друзья", 420, 452, 760, v => addName = v);
            var submit = Button("Добавить и открыть  →", 420, 566, 400, 60, SubmitAddress, true);
            submit.name = "AddServer.Save";
            Button("← Назад", 836, 566, 344, 60, ShowRooms);
            status = Label(content.transform, serverMessage, 420, 646, 760, 30, 14, Accent);
        }

        void SubmitAddress()
        {
            string host; int port; string error;
            if (!ServerAddressRules.TryParse(addAddress, addPort, out host, out port, out error))
            {
                serverMessage = error;
                ShowAddServer(false);
                return;
            }
            if (game.Servers.IndexOf(host, port) < 0)
            {
                int added = game.Servers.Add(new ServerAddress { host = host, port = port, name = string.IsNullOrWhiteSpace(addName) ? ServerAddressRules.SuggestName(host) : addName.Trim() });
                if (added < 0) { serverMessage = "Список адресов заполнен — удалите лишний"; ShowAddServer(false); return; }
            }
            game.SaveServers();
            int index = game.Servers.IndexOf(host, port);
            serverSelection = index;
            serverMessage = "";
            SelectServer(index);
        }

        void SubmitHost()
        {
            string name = string.IsNullOrWhiteSpace(addName) ? "Мир " + game.PlayerName : addName.Trim();
            game.HostNewWorld(name, error =>
            {
                serverMessage = error;
                if (page == Page.AddServer) ShowAddServer(true);
            });
        }

        /// <summary>Address card shown in the lobby so friends know where to connect.</summary>
        void LobbyAddressCard(Transform parent, float x, float y, float w)
        {
            var card = Box(parent, x, y, w, 132, new Color(.045f, .075f, .088f, .98f));
            Label(card.transform, "АДРЕС СЕРВЕРА", 22, 16, w - 44, 24, 13, Accent, true);
            Label(card.transform, game.EndpointAddress, 22, 44, w - 44, 34, 22, White, true);
            string world = string.IsNullOrEmpty(game.HostedServerName) ? "Мир создан этим лобби" : game.HostedServerName;
            Label(card.transform, "Мир: " + world + "  ·  Мультиплеер → Добавить сервер", 22, 88, w - 44, 26, 14, Muted);
        }
    }
}
