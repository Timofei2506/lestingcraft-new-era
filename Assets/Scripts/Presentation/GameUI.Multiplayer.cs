using System;
using UnityEngine;
using UnityEngine.UI;
using LestingCraft.Gameplay;
using LestingCraft.Networking;

namespace LestingCraft.Presentation
{
    /// <summary>
    /// Full Multiplayer Hub & Server Studio:
    /// - Quick Join by 6-char room code, 32-char code, or IP:Port
    /// - Live UDP LAN Auto-Discovery Radar for local worlds over Wi-Fi / LAN / VPN
    /// - Saved Server Address Book + 1-click Official Cloud Hub preset
    /// - Full Server Creator Studio (Cloud 24/7 or Local LAN, Game Mode, Seed Presets, Max Players, Dedicated Node CLI)
    /// </summary>
    public sealed partial class GameUI
    {
        int serverSelection = -1;
        PublicServer[] serverWorlds;
        string serverMessage = "";
        bool serverLoading;
        string addAddress = "", addPort = "443", addName = "";
        string quickJoinInput = "";
        bool hostUseLan;
        int hostMode;
        string hostSeedText = "2506";
        int hostMaxPlayers = 8;

        public void ShowRooms()
        {
            Clear(Page.Rooms);
            Header("Многопользовательская игра", false);
            LanDiscoveryRadar.EnsureListening();
            var book = game.Servers;
            var lanWorlds = LanDiscoveryRadar.ActiveWorlds();

            // 1. Top Quick-Join Bar (Code or IP:Port)
            var quickBar = Box(content.transform, 64, 128, 1472, 76, Panel);
            Label(quickBar.transform, "БЫСТРЫЙ ВХОД ПО КОДУ ИЛИ IP", 22, 10, 420, 20, 12, Accent, true);
            Label(quickBar.transform, "Вставьте 6-значный код комнаты, IP:порт (192.168.1.7:8787) или домен:", 22, 32, 520, 26, 13, Muted);
            var qInput = InputIn(quickBar.transform, "", quickJoinInput, "Например A4F8B2 или 192.168.1.7:8787", 548, -2, 520, v => quickJoinInput = v, 64);
            qInput.name = "Rooms.QuickInput";
            var qBtn = Button("Войти сразу  →", 1084, 12, 224, 52, () =>
            {
                serverMessage = "Подключаюсь…";
                game.QuickJoinByCodeOrAddress(quickJoinInput, err =>
                {
                    serverMessage = err;
                    if (page == Page.Rooms) ShowRooms();
                });
            }, true, quickBar.transform);
            qBtn.name = "Rooms.QuickJoin";
            Button("+ Облачный Хаб", 1320, 12, 134, 52, () =>
            {
                int idx = game.EnsureOfficialCloudServerAdded();
                SelectServer(idx);
            }, false, quickBar.transform);

            // 2. Left Top: Saved Server Address Book
            var left = Box(content.transform, 64, 218, 744, 304, Panel);
            Label(left.transform, "МОИ СЕРВЕРА (ИНТЕРНЕТ И IP)", 24, 14, 500, 26, 16, Accent, true);
            Label(left.transform, book.Count + " / " + ServerBook.Capacity, 550, 14, 170, 26, 14, Muted, false, TextAnchor.MiddleRight);
            if (book.Count == 0)
            {
                Label(left.transform, "Список пуст. Нажмите «+ Облачный Хаб» сверху для игры через Интернет\nили «Добавить сервер», чтобы сохранить любой IP:порт.", 24, 54, 696, 60, 15, White);
            }
            for (int i = 0; i < Mathf.Min(book.Count, 4); i++)
            {
                int index = i;
                var entry = book.Servers[i];
                bool chosen = serverSelection == index;
                var row = Button("", 20, 48 + i * 60, 646, 54, () => SelectServer(index), chosen, left.transform);
                Label(row.transform, entry.Title, 16, 6, 380, 24, 17, chosen ? Ink : White, true);
                Label(row.transform, entry.Address, 16, 29, 380, 20, 13, chosen ? Ink : Muted);
                string population = entry.lastPlayers < 0 ? "проверить →" : ("Онлайн: " + entry.lastPlayers);
                Label(row.transform, population, 430, 14, 196, 24, 14, chosen ? Ink : Accent, false, TextAnchor.MiddleRight);

                Button("✕", 674, 48 + i * 60, 50, 54, () =>
                {
                    book.RemoveAt(index);
                    game.SaveServers();
                    if (serverSelection >= book.Count) serverSelection = book.Count - 1;
                    ShowRooms();
                }, false, left.transform);
            }

            // 3. Left Bottom: Live Local Network (LAN) Auto-Discovery Radar!
            var lanBox = Box(content.transform, 64, 534, 744, 212, Panel);
            Label(lanBox.transform, "📡 ЛОКАЛЬНЫЕ МИРЫ В СЕТИ (LAN / WI-FI / VPN)", 24, 14, 520, 26, 16, SpringAccent, true);
            Button("Обновить LAN", 584, 10, 140, 34, ShowRooms, false, lanBox.transform);

            if (game.IsHostingLan)
            {
                var activeLan = LocalLanServer.Active;
                var hostBanner = Box(lanBox.transform, 20, 50, 704, 64, new Color(.08f, .26f, .20f, .92f));
                Label(hostBanner.transform, "✓ ВАШ LAN-СЕРВЕР АКТИВЕН: " + activeLan.ShareAddress + "  ·  Код: " + activeLan.ShortCode, 16, 8, 460, 24, 15, SpringAccent, true);
                Label(hostBanner.transform, "Мир «" + activeLan.WorldName + "» виден всем в локальной сети автоматически!", 16, 33, 460, 22, 13, Muted);
                Button("Копировать IP", 476, 11, 132, 42, () =>
                {
                    GUIUtility.systemCopyBuffer = activeLan.ShareAddress;
                    serverMessage = "Скопирован локальный адрес: " + activeLan.ShareAddress;
                    ShowRooms();
                }, true, hostBanner.transform);
                Button("Стоп", 616, 11, 76, 42, () =>
                {
                    game.StopLocalLanServer();
                    serverMessage = "Локальный LAN-сервер остановлен.";
                    ShowRooms();
                }, false, hostBanner.transform);
            }
            else if (lanWorlds.Length == 0)
            {
                Label(lanBox.transform, "Активных LAN-миров поблизости пока не обнаружено.\nНажмите «Запустить LAN-мир» внизу или откройте любой одиночный мир через Паузу [ESC]!\nВаш локальный IP: " + game.ActiveLanAddress,
                    24, 54, 696, 128, 14, Muted);
            }

            int lanStartY = game.IsHostingLan ? 122 : 50;
            int maxLanRows = game.IsHostingLan ? 1 : 2;
            for (int i = 0; i < Mathf.Min(lanWorlds.Length, maxLanRows); i++)
            {
                var lw = lanWorlds[i];
                var row = Box(lanBox.transform, 20, lanStartY + i * 74, 704, 66, new Color(.05f, .15f, .22f, .94f));
                Label(row.transform, lw.title + "  ·  @" + lw.hostName, 16, 8, 430, 24, 17, White, true);
                Label(row.transform, lw.DisplayAddress + "  ·  " + GameRules.ModeNames[Mathf.Clamp(lw.mode, 0, 3)] + "  ·  Игроков: " + lw.players + "/" + lw.maxPlayers + "  ·  Код " + lw.ShortCode,
                    16, 35, 470, 22, 13, Muted);
                Button("Войти в LAN  →", 506, 10, 184, 46, () =>
                {
                    serverMessage = "";
                    game.JoinPublishedWorld(lw.EndpointUrl, lw.code, lw.title);
                }, true, row.transform);
            }

            // 4. Right Column: Selected Server's Published Worlds
            var right = Box(content.transform, 832, 218, 704, 528, Panel);
            if (serverSelection < 0 || serverSelection >= book.Count)
            {
                Label(right.transform, "ОПУБЛИКОВАННЫЕ МИРЫ НА СЕРВЕРЕ", 26, 18, 640, 28, 16, Accent, true);
                Label(right.transform, "Выберите сервер слева (или нажмите «+ Облачный Хаб»), чтобы увидеть все открытые миры игроков и присоединиться в 1 клик.\n\n• Хотите свой облачный или LAN-сервер?\n  Нажмите «Создать свой сервер» — выбор режима, зерна мира и лимита игроков.\n\n• Играете в одиночном мире и хотите позвать друга?\n  Откройте меню паузы [ESC] → «Открыть мир по LAN» или «Создать сервер» — все ваши постройки сохранятся!",
                    26, 62, 652, 340, 16, White);
            }
            else
            {
                var entry = book.Servers[serverSelection];
                Label(right.transform, entry.Title.ToUpperInvariant(), 26, 16, 460, 30, 21, White, true);
                Label(right.transform, entry.Address + "   ·   " + (serverLoading ? "обновляю список…" : (serverWorlds == null ? "нажмите Обновить" : serverWorlds.Length + " открытых мир(ов)")),
                    26, 48, 480, 24, 14, Muted);
                var refresh = Button("Обновить", 542, 16, 138, 46, RefreshSelectedServer, false, right.transform);
                refresh.name = "Rooms.Refresh";

                for (int i = 0; i < (serverWorlds == null ? 0 : Mathf.Min(serverWorlds.Length, 6)); i++)
                {
                    var world = serverWorlds[i];
                    string title = string.IsNullOrEmpty(world.name) ? "Мир" : world.name;
                    string shortCode = !string.IsNullOrEmpty(world.code) && world.code.Length >= 6 ? world.code.Substring(0, 6).ToUpperInvariant() : "";
                    var row = Button("", 22, 82 + i * 70, 660, 62, () => JoinWorld(entry, world), false, right.transform);
                    Label(row.transform, title, 18, 7, 420, 26, 18, White, true);
                    Label(row.transform, GameRules.ModeNames[Mathf.Clamp(world.mode, 0, 3)] + "  ·  " + world.PopulationText + (shortCode.Length > 0 ? "  ·  Код: " + shortCode : ""),
                        18, 34, 450, 22, 13, Muted);
                    Label(row.transform, "Войти  →", 494, 18, 148, 26, 16, Accent, false, TextAnchor.MiddleRight);
                }
                if (!serverLoading && serverWorlds != null && serverWorlds.Length == 0)
                    Label(right.transform, "На этом сервере пока нет опубликованных миров.\nНажмите «Создать свой сервер» внизу — и ваш мир станет первым!", 26, 98, 640, 90, 16, White);
            }

            // 5. Bottom Action Bar (preserving Rooms.Add, Rooms.Host, Rooms.Back!)
            var add = Button("Добавить сервер", 64, 764, 270, 56, ShowAddServer);
            add.name = "Rooms.Add";
            var host = Button("Создать свой сервер  →", 348, 764, 340, 56, HostFromMenu, true);
            host.name = "Rooms.Host";
            var lanQuick = Button("📡 Запустить LAN-мир", 702, 764, 290, 56, () =>
            {
                hostUseLan = true;
                ShowAddServer(true);
            });
            lanQuick.name = "Rooms.LanHost";
            Button("Друзья", 1006, 764, 180, 56, OpenFriendsHub);
            var back = Button("← В главное меню", 1200, 764, 336, 56, ShowHome);
            back.name = "Rooms.Back";

            status = Label(content.transform,
                !string.IsNullOrEmpty(serverMessage) ? serverMessage : "Локальные миры по LAN находятся автоматически (порт 8787 / UDP 47777), облачные — по HTTPS (порт 443).",
                64, 832, 1472, 28, 14, !string.IsNullOrEmpty(serverMessage) ? Accent : Muted);
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
            hostMode = game.SelectedMode;
            ShowAddServer(true);
        }

        public void ShowAddServer()
        {
            ShowAddServer(false);
        }

        void ShowAddServer(bool hosting)
        {
            Clear(Page.AddServer);
            Header(hosting ? "Студия создания сервера" : "Добавить сервер", false);
            if (!hosting)
            {
                Label(content.transform, "ДОБАВИТЬ АДРЕС СЕРВЕРА", 64, 150, 900, 44, 32, White, true);
                Label(content.transform, "Введите IP или домен и порт. Для локальных серверов друзей порт обычно 8787, для HTTPS — 443.", 66, 196, 1400, 36, 16, Muted);

                var card = Box(content.transform, 380, 260, 840, 390, Panel);
                Label(card.transform, "ПАРАМЕТРЫ ПОДКЛЮЧЕНИЯ", 40, 24, 700, 28, 17, Accent, true);
                Input("АДРЕС СЕРВЕРА", addAddress, "example.com или 192.168.1.7:8787", 420, 320, 560, v => addAddress = v);
                Input("ПОРТ", addPort, "443", 1010, 320, 170, v => addPort = v);
                Input("НАЗВАНИЕ В СПИСКЕ", addName, "Сервер друзей", 420, 442, 760, v => addName = v);
                var submit = Button("Добавить и открыть  →", 420, 564, 420, 60, SubmitAddress, true);
                submit.name = "AddServer.Save";
                Button("← Назад к серверам", 856, 564, 324, 60, ShowRooms);
                status = Label(content.transform, serverMessage, 420, 662, 760, 30, 14, Accent);
                return;
            }

            // FULL SERVER CREATOR STUDIO!
            var studio = Box(content.transform, 140, 138, 1320, 660, Panel);
            Label(studio.transform, "СОЗДАТЬ СВОЙ СЕРВЕР ИЛИ ЛОКАЛЬНЫЙ СЕТЕВОЙ МИР", 36, 22, 920, 32, 22, White, true);
            Label(studio.transform, "Запустите облачный сервер 24/7 или встроенный LAN-сервер прямо на вашем ПК в 1 клик.", 36, 56, 980, 24, 15, Muted);

            // Hosting Backend Toggle: Cloud vs LAN
            Label(studio.transform, "1. ТИП ХОСТИНГА", 36, 98, 400, 24, 13, Accent, true);
            Button("☁ Облачный сервер (Интернет · " + game.EndpointAddress + ")", 36, 126, 610, 54, () => { hostUseLan = false; ShowAddServer(true); }, !hostUseLan, studio.transform);
            Button("📡 Локальная сеть LAN / VPN (" + LocalLanServer.DetectLocalIPv4() + ":8787)", 662, 126, 622, 54, () => { hostUseLan = true; ShowAddServer(true); }, hostUseLan, studio.transform);

            // Server Name & Seed
            string defaultTitle = "Мир @" + (Accounts != null && Accounts.Logged && Accounts.Profile != null ? Accounts.Profile.username : ProfileAvatar.LocalNickname);
            InputIn(studio.transform, "2. НАЗВАНИЕ СЕРВЕРА", addName, defaultTitle, 36, 200, 610, v => addName = v, 32);
            InputIn(studio.transform, "3. ЗЕРНО МИРА (SEED)", hostSeedText, "2506", 662, 200, 250, v => hostSeedText = v, 14);

            // Seed Presets
            Label(studio.transform, "БЫСТРЫЕ ПРЕСЕТЫ МИРА", 928, 200, 350, 20, 12, Accent, true);
            Button("Острова", 928, 224, 112, 52, () => { hostSeedText = "2506"; ShowAddServer(true); }, hostSeedText == "2506", studio.transform);
            Button("Врата Ада", 1046, 224, 118, 52, () => { hostSeedText = "6660"; ShowAddServer(true); }, hostSeedText == "6660", studio.transform);
            Button("Случайный", 1170, 224, 114, 52, () => { hostSeedText = UnityEngine.Random.Range(1000, 999999).ToString(); ShowAddServer(true); }, false, studio.transform);

            // Game Mode Picker
            Label(studio.transform, "4. РЕЖИМ ИГРЫ НА СЕРВЕРЕ", 36, 306, 500, 24, 13, Accent, true);
            for (int m = 0; m < 4; m++)
            {
                int modeIdx = m;
                Button(GameRules.ModeNames[m], 36 + m * 314, 334, 302, 54, () => { hostMode = modeIdx; game.SelectedMode = modeIdx; ShowAddServer(true); }, hostMode == modeIdx, studio.transform);
            }

            // Max Players (for LAN) & Dedicated Server CLI helper
            Label(studio.transform, "5. ЛИМИТ ИГРОКОВ И ВЫДЕЛЕННЫЙ ХОСТИНГ", 36, 410, 600, 24, 13, Accent, true);
            int[] caps = { 4, 8, 12, 16 };
            for (int ci = 0; ci < caps.Length; ci++)
            {
                int capVal = caps[ci];
                Button(capVal + " игр.", 36 + ci * 144, 438, 134, 48, () => { hostMaxPlayers = capVal; ShowAddServer(true); }, hostMaxPlayers == capVal, studio.transform);
            }

            Button("📋 Скопировать команду своего VPS / Node-сервера", 662, 438, 622, 48, () =>
            {
                GUIUtility.systemCopyBuffer = "PORT=8787 node Server/src/node.js";
                serverMessage = "Скопировано: PORT=8787 node Server/src/node.js — запуск круглосуточного выделенного сервера!";
                ShowAddServer(true);
            }, false, studio.transform);

            string infoLine = hostUseLan
                ? "✓ LAN-сервер запустится прямо внутри игры на порту 8787, скопирует ваш локальный IP в буфер и станет автоматически виден друзьям по Wi-Fi / LAN / Radmin / Tailscale!"
                : "✓ Облачный мир опубликуется на " + game.EndpointAddress + " — друзья смогут зайти через Интернет по списку миров или короткому 6-значному коду!";
            Label(studio.transform, infoLine, 36, 504, 1248, 44, 14, Muted);

            var create = Button(hostUseLan ? "📡 Запустить LAN-сервер и войти  →" : "☁ Создать облачный сервер и войти  →",
                36, 566, 560, 64, SubmitHost, true, studio.transform);
            create.name = "AddServer.Create";
            Button("← Назад к списку серверов", 614, 566, 340, 64, ShowRooms, false, studio.transform);

            status = Label(content.transform, serverMessage, 140, 814, 1320, 30, 15, Accent);
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
            string fallback = "Мир @" + (Accounts != null && Accounts.Logged && Accounts.Profile != null ? Accounts.Profile.username : ProfileAvatar.LocalNickname);
            string name = string.IsNullOrWhiteSpace(addName) ? fallback : addName.Trim();
            if (!int.TryParse((hostSeedText ?? "").Trim(), out int parsedSeed))
                parsedSeed = (hostSeedText ?? "2506").GetHashCode();

            if (hostUseLan)
            {
                game.HostNewLanWorld(name, hostMode, parsedSeed, hostMaxPlayers, error =>
                {
                    serverMessage = error;
                    if (page == Page.AddServer) ShowAddServer(true);
                });
            }
            else
            {
                game.HostNewCloudWorldAdvanced(name, hostMode, parsedSeed, error =>
                {
                    serverMessage = error;
                    if (page == Page.AddServer) ShowAddServer(true);
                });
            }
        }

        /// <summary>Address card shown in the lobby so friends know where to connect.</summary>
        void LobbyAddressCard(Transform parent, float x, float y, float w)
        {
            var card = Box(parent, x, y, w, 132, new Color(.045f, .125f, .195f, .96f));
            string kindLabel = game.IsHostingLan ? "📡 ЛОКАЛЬНЫЙ СЕРВЕР (LAN) · ВИДЕН В СЕТИ АВТОМАТИЧЕСКИ" : "☁ АДРЕС СЕРВЕРА И КОД БЫСТРОГО ВХОДА";
            Label(card.transform, kindLabel, 22, 14, w - 44, 24, 13, game.IsHostingLan ? SpringAccent : Accent, true);
            string addr = game.IsHostingLan ? game.ActiveLanAddress : game.EndpointAddress;
            string shortCode = !string.IsNullOrEmpty(game.RoomCode) && game.RoomCode.Length >= 6 ? ("   ·   Быстрый код: " + game.RoomCode.Substring(0, 6).ToUpperInvariant()) : "";
            Label(card.transform, addr + shortCode, 22, 44, w - 340, 34, 22, White, true);
            string world = string.IsNullOrEmpty(game.HostedServerName) ? "Мир создан этим лобби" : game.HostedServerName;
            Label(card.transform, "Мир: " + world + "  ·  Друзья могут зайти по быстрому коду или через список серверов", 22, 88, w - 340, 26, 14, Muted);
            Button("Копировать IP / Код", w - 290, 36, 264, 56, () => GUIUtility.systemCopyBuffer = addr, false, card.transform);
        }
    }
}
