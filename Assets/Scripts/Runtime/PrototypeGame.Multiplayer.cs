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
    }
}
