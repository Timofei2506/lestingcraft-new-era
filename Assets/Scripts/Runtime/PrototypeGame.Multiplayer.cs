using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Networking;
using LestingCraft.Gameplay;
using LestingCraft.World;

namespace LestingCraft.Runtime
{
    /// <summary>
    /// Minecraft style multiplayer: the player owns an address book of servers
    /// (IP + port), browses the worlds published there and can open the world he
    /// is playing in as a server with one button in the pause menu.
    /// </summary>
    public sealed partial class PrototypeGame
    {
        public const string ServerBookKey = "LC.Servers.V1";
        const int MaxUploadedEdits = 4000;

        ServerBook serverBook;
        ServerBrowser browser;
        string hostedServerName = "";
        string hostedServerCode = "";
        SavedBlock[] pendingUpload;
        bool lobbyAutoStart;

        public ServerBook Servers
        {
            get
            {
                if (serverBook == null) serverBook = ServerBook.FromJson(PlayerPrefs.GetString(ServerBookKey, string.Empty));
                return serverBook;
            }
        }

        public void SaveServers()
        {
            PlayerPrefs.SetString(ServerBookKey, Servers.ToJson());
            PlayerPrefs.Save();
        }

        public string Endpoint { get { return endpoint; } }
        public string HostedServerName { get { return hostedServerName; } }
        public bool HasHostedServer { get { return !string.IsNullOrEmpty(hostedServerCode); } }
        public bool AutoStartLobby { get { return lobbyAutoStart; } set { lobbyAutoStart = value; } }

        /// <summary>The address (host:port) other players type into "Добавить сервер".</summary>
        public string EndpointAddress
        {
            get
            {
                string host; int port; string error;
                if (ServerAddressRules.TryParse(endpoint, string.Empty, out host, out port, out error))
                    return ServerAddressRules.Display(host, port);
                return string.IsNullOrEmpty(endpoint) ? "адрес не настроен" : endpoint;
            }
        }

        ServerBrowser Browser
        {
            get
            {
                if (!browser)
                {
                    var host = new GameObject("Server browser");
                    host.transform.SetParent(transform, false);
                    browser = host.AddComponent<ServerBrowser>();
                }
                return browser;
            }
        }

        public void BrowseServers(ServerAddress entry, Action<PublicServer[]> done, Action<string> failed)
        {
            if (entry == null) { if (failed != null) failed("Выберите сервер из списка"); return; }
            Browser.List(entry.Url, done, failed);
        }

        /// <summary>Join a world that is already published on a server.</summary>
        public void JoinPublishedWorld(string serverEndpoint, string roomCode, string worldName)
        {
            if (world) return;
            hostedServerCode = roomCode;
            hostedServerName = worldName;
            lobbyAutoStart = false;
            if (!string.IsNullOrEmpty(serverEndpoint)) endpoint = serverEndpoint.TrimEnd('/');
            code = roomCode;
            JoinRoomInternal();
        }

        /// <summary>
        /// Opens the world the player is standing in as a server. Local builds are
        /// uploaded into the shared world after the connection settles.
        /// </summary>
        public void OpenWorldAsServer(string serverName)
        {
            if (busy) return;
            if (!world || !Session) { HostNewWorld(serverName, null); return; }
            if (world.Dimension != Dimension.Lestingia)
            {
                Session.Notice = "Открыть для друзей можно только мир Лестингии — вернитесь на планету";
                return;
            }
            var local = world.ExportEdits();
            var keep = new List<SavedBlock>();
            if (local != null)
                foreach (var edit in local)
                    if (edit.realm == 0 && keep.Count < MaxUploadedEdits) keep.Add(edit);
            int seed = world.Seed, mode = (int)Session.Mode;
            string name = string.IsNullOrWhiteSpace(serverName) ? "Мир " + playerName : serverName;
            busy = true;
            Session.Notice = "Создаю сервер…";
            Publish(seed, mode, name,
                server =>
                {
                    pendingUpload = keep.Count > 0 ? keep.ToArray() : null;
                    hostedServerName = server.name;
                    hostedServerCode = server.code;
                    lobbyAutoStart = true;
                    code = server.code;
                    ReturnToMenu();
                    StartCoroutine(JoinAfterLeave());
                },
                error =>
                {
                    busy = false;
                    if (Session) Session.Notice = error;
                    else Fail(error);
                });
        }

        /// <summary>Creates a brand new hosted world from the multiplayer screen.</summary>
        public void HostNewWorld(string serverName, Action<string> failed)
        {
            if (busy) return;
            if (!ValidateEndpoint()) { if (failed != null) failed("Сначала укажите адрес сервера"); return; }
            int seed = Guid.NewGuid().GetHashCode();
            seedText = seed.ToString();
            string name = string.IsNullOrWhiteSpace(serverName) ? "Новый мир" : serverName;
            busy = true;
            Publish(seed, selectedMode, name,
                server =>
                {
                    pendingUpload = null;
                    hostedServerName = server.name;
                    hostedServerCode = server.code;
                    lobbyAutoStart = true;
                    code = server.code;
                    JoinRoomInternal();
                },
                error => { busy = false; if (failed != null) failed(error); else Fail(error); });
        }

        void Publish(int seed, int mode, string name, Action<PublicServer> done, Action<string> failed)
        {
            if (!ValidateEndpoint()) { busy = false; if (failed != null) failed("Укажите https:// адрес сервера"); return; }
            Browser.Publish(endpoint, name, seed, mode, PlayerKey(), done, failed);
        }

        IEnumerator JoinAfterLeave()
        {
            // ReturnToMenu tears the world down in its own coroutine; wait until the
            // world is really gone before asking the room to start.
            float deadline = Time.realtimeSinceStartup + 6f;
            while (world && Time.realtimeSinceStartup < deadline) yield return null;
            busy = false;
            JoinRoomInternal();
        }

        /// <summary>Called when the shared world is ready: push local builds into it.</summary>
        void BeginHostMigration()
        {
            if (pendingUpload == null || pendingUpload.Length == 0) return;
            StartCoroutine(UploadLocalEdits(pendingUpload));
            pendingUpload = null;
        }

        IEnumerator UploadLocalEdits(SavedBlock[] edits)
        {
            if (Session) Session.Notice = "Переношу ваши постройки в общий мир: 0/" + edits.Length;
            int sent = 0;
            for (int i = 0; i < edits.Length; i++)
            {
                if (!client || !client.Connected) yield break;
                var edit = edits[i];
                client.Send(new NetPacket { type = "edit", x = edit.x, y = edit.y, z = edit.z, block = edit.block });
                sent++;
                if (sent % 8 == 0)
                {
                    if (Session) Session.Notice = "Переношу ваши постройки в общий мир: " + sent + "/" + edits.Length;
                    yield return new WaitForSecondsRealtime(.14f);
                }
            }
            if (Session) Session.Notice = "Постройки перенесены в общий мир (" + sent + ")";
        }

        /// <summary>The server address of a published world, ready to be shared.</summary>
        public string ShareAddress
        {
            get { return EndpointAddress + (string.IsNullOrEmpty(hostedServerName) ? string.Empty : "  ·  " + hostedServerName); }
        }

        /// <summary>Where a new player downloads the game. Shown in the invite text.</summary>
        public const string WebsiteUrl = "https://lestingcraft.kroterallitir.workers.dev";

        /// <summary>
        /// A message a host can paste into any chat. It never contains a room code:
        /// the friend downloads the game, opens "Мультиплеер", adds the address and
        /// sees the world in the list.
        /// </summary>
        public string InviteText
        {
            get
            {
                string world = string.IsNullOrEmpty(hostedServerName) ? "мой мир" : "«" + hostedServerName + "»";
                if (IsHostingLan)
                {
                    var lan = LocalLanServer.Active;
                    return "Заходи в мой локальный мир LestingCraft — " + world + "!\n" +
                           "• В одной сети (Wi-Fi / LAN / Radmin / Tailscale): открой «Многопользовательская игра» — мир появится в блоке «Локальная сеть (LAN)» автоматически!\n" +
                           "• Или введи адрес вручную: " + lan.ShareAddress + "  (код: " + lan.ShortCode + ")";
                }
                string shortTag = !string.IsNullOrEmpty(hostedServerCode) && hostedServerCode.Length >= 6
                    ? "  (быстрый код: " + hostedServerCode.Substring(0, 6).ToUpperInvariant() + ")"
                    : "";
                return "Заходи в LestingCraft — " + world + ".\n" +
                       "1. Скачай игру: " + WebsiteUrl + "\n" +
                       "2. Мультиплеер → «Быстрый вход» или «Добавить сервер»\n" +
                       "3. Адрес: " + EndpointAddress + shortTag + "\n" +
                       "Мир появится в списке, останется нажать «Войти».";
            }
        }

        public bool IsHostingLan => LocalLanServer.Active != null && LocalLanServer.Active.IsRunning;
        public string ActiveLanAddress => IsHostingLan ? LocalLanServer.Active.ShareAddress : LocalLanServer.DetectLocalIPv4() + ":8787";

        public int EnsureOfficialCloudServerAdded()
        {
            const string cloudHost = "lestingcraft.kroterallitir.workers.dev";
            int idx = Servers.Add(new ServerAddress
            {
                host = cloudHost,
                port = 443,
                name = "Официальный Облачный Хаб"
            });
            SaveServers();
            if (string.IsNullOrEmpty(endpoint)) endpoint = "https://" + cloudHost;
            return idx;
        }

        /// <summary>Opens the currently running local singleplayer world on the embedded LAN server immediately.</summary>
        public void OpenLocalWorldToLan(string serverName = null, int maxPlayers = 8)
        {
            if (busy) return;
            if (!world || !Session)
            {
                HostNewLanWorld(serverName, selectedMode, Guid.NewGuid().GetHashCode(), maxPlayers, Fail);
                return;
            }
            if (world.Dimension != Dimension.Lestingia)
            {
                Session.Notice = "Открыть локальный мир по LAN можно на планете Лестингия — вернитесь через портал или шаттл";
                return;
            }
            var localEdits = world.ExportEdits();
            int seed = world.Seed;
            int mode = (int)Session.Mode;
            string hostNick = Accounts != null && Accounts.Logged && Accounts.Profile != null
                ? Accounts.Profile.username
                : LestingCraft.Presentation.ProfileAvatar.LocalNickname;
            string title = string.IsNullOrWhiteSpace(serverName) ? ("Мир @" + hostNick) : serverName.Trim();

            var lan = LocalLanServer.StartOrReplace(title, hostNick, seed, mode, maxPlayers, localEdits);
            if (lan == null)
            {
                Session.Notice = "Не удалось открыть порт LAN (в WebGL используйте «Опубликовать облачный сервер»)";
                return;
            }

            RealmFeaturesAndMarket.AddContractProgress("coop_host", 1);
            pendingUpload = null; // All local edits are already preloaded directly into LocalLanServer!
            hostedServerName = lan.WorldName;
            hostedServerCode = lan.RoomCode;
            lobbyAutoStart = true;
            endpoint = lan.ConnectLocalUrl;
            code = lan.RoomCode;
            GUIUtility.systemCopyBuffer = lan.ShareAddress;
            busy = true;
            ReturnToMenu();
            StartCoroutine(JoinAfterLeave());
        }

        /// <summary>Creates and joins a brand-new local LAN server directly from the Multiplayer Studio.</summary>
        public void HostNewLanWorld(string serverName, int mode, int seed, int maxPlayers, Action<string> failed)
        {
            if (busy) return;
            string hostNick = Accounts != null && Accounts.Logged && Accounts.Profile != null
                ? Accounts.Profile.username
                : LestingCraft.Presentation.ProfileAvatar.LocalNickname;
            string title = string.IsNullOrWhiteSpace(serverName) ? ("LAN Мир @" + hostNick) : serverName.Trim();
            var lan = LocalLanServer.StartOrReplace(title, hostNick, seed, mode, maxPlayers, null);
            if (lan == null)
            {
                string err = "Локальный LAN-сервер доступен в настольной сборке ПК (для браузера выберите Облачный сервер)";
                if (failed != null) failed(err); else Fail(err);
                return;
            }

            RealmFeaturesAndMarket.AddContractProgress("coop_host", 1);
            selectedMode = Mathf.Clamp(mode, 0, 3);
            seedText = seed.ToString();
            pendingUpload = null;
            hostedServerName = lan.WorldName;
            hostedServerCode = lan.RoomCode;
            lobbyAutoStart = true;
            endpoint = lan.ConnectLocalUrl;
            code = lan.RoomCode;
            GUIUtility.systemCopyBuffer = lan.ShareAddress;
            JoinRoomInternal();
        }

        /// <summary>Creates a new Cloud server with explicit mode and seed.</summary>
        public void HostNewCloudWorldAdvanced(string serverName, int mode, int seed, Action<string> failed)
        {
            if (busy) return;
            if (string.IsNullOrEmpty(endpoint))
                endpoint = WebsiteUrl;
            if (!ValidateEndpoint()) { if (failed != null) failed("Сначала укажите https:// адрес облачного сервера"); return; }
            selectedMode = Mathf.Clamp(mode, 0, 3);
            seedText = seed.ToString();
            string name = string.IsNullOrWhiteSpace(serverName) ? "Новый сервер" : serverName.Trim();
            busy = true;
            RealmFeaturesAndMarket.AddContractProgress("coop_host", 1);
            Publish(seed, selectedMode, name,
                server =>
                {
                    pendingUpload = null;
                    hostedServerName = server.name;
                    hostedServerCode = server.code;
                    lobbyAutoStart = true;
                    code = server.code;
                    JoinRoomInternal();
                },
                error => { busy = false; if (failed != null) failed(error); else Fail(error); });
        }

        public void StopLocalLanServer()
        {
            LocalLanServer.StopActive();
        }

        /// <summary>One-click join by 6-char short room code, 32-char room code, or IP:Port / domain!</summary>
        public void QuickJoinByCodeOrAddress(string rawInput, Action<string> failed)
        {
            if (busy || world) return;
            string clean = (rawInput ?? "").Trim();
            if (clean.Length == 0) { if (failed != null) failed("Введите IP:Порт или код комнаты"); return; }

            // 1. Exact 32-char hex room code
            if (clean.Length == 32 && clean.IndexOf('.') < 0 && clean.IndexOf(':') < 0)
            {
                if (string.IsNullOrEmpty(endpoint)) endpoint = WebsiteUrl;
                JoinPublishedWorld(endpoint, clean.ToLowerInvariant(), "Комната");
                return;
            }

            // 2. Short 4..10 char code -> match active LAN beacons first, then query current cloud endpoint!
            if (clean.Length >= 4 && clean.Length <= 10 && clean.IndexOf('.') < 0 && clean.IndexOf(':') < 0)
            {
                string prefix = clean.ToLowerInvariant();
                foreach (var lan in LanDiscoveryRadar.ActiveWorlds())
                {
                    if (!string.IsNullOrEmpty(lan.code) && lan.code.ToLowerInvariant().StartsWith(prefix, StringComparison.Ordinal))
                    {
                        JoinPublishedWorld(lan.EndpointUrl, lan.code, lan.title);
                        return;
                    }
                }
                string cloudUrl = string.IsNullOrEmpty(endpoint) ? WebsiteUrl : endpoint;
                Browser.List(cloudUrl,
                    worlds =>
                    {
                        if (worlds != null)
                        {
                            foreach (var w in worlds)
                            {
                                if (w != null && !string.IsNullOrEmpty(w.code) && w.code.ToLowerInvariant().StartsWith(prefix, StringComparison.Ordinal))
                                {
                                    JoinPublishedWorld(cloudUrl, w.code, w.name);
                                    return;
                                }
                            }
                        }
                        if (failed != null) failed("Комната с кодом «" + clean.ToUpperInvariant() + "» не найдена");
                    },
                    err => { if (failed != null) failed("Не удалось проверить код: " + err); });
                return;
            }

            // 3. IP:Port or Domain -> parse, save in ServerBook, fetch worlds, and auto-join first active world!
            if (ServerAddressRules.TryParse(clean, "", out string host, out int port, out string parseErr))
            {
                var addr = new ServerAddress { host = host, port = port, name = ServerAddressRules.SuggestName(host) };
                Servers.Add(addr);
                SaveServers();
                Browser.List(addr.Url,
                    worlds =>
                    {
                        if (worlds != null && worlds.Length > 0 && !string.IsNullOrEmpty(worlds[0].code))
                            JoinPublishedWorld(addr.Url, worlds[0].code, worlds[0].name);
                        else if (failed != null)
                            failed("Сервер " + addr.Address + " добавлен в список, но там пока нет открытых миров");
                    },
                    err => { if (failed != null) failed("Сервер " + addr.Address + " не ответил: " + err); });
                return;
            }

            if (failed != null) failed(parseErr);
        }
    }
}
