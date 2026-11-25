using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using LestingCraft.Gameplay;

namespace LestingCraft.Networking
{
    [Serializable]
    public sealed class LanBeaconInfo
    {
        public string magic = "LC_LAN_V1";
        public string title = "Локальный мир";
        public string hostName = "Player";
        public string ip = "127.0.0.1";
        public int port = 8787;
        public string code = "";
        public int mode;
        public int seed = 2506;
        public int players = 1;
        public int maxPlayers = 8;
        [NonSerialized] public double lastSeenRealtime;

        public string EndpointUrl => "http://" + ip + ":" + port;
        public string DisplayAddress => ip + ":" + port;
        public string ShortCode => !string.IsNullOrEmpty(code) && code.Length >= 6 ? code.Substring(0, 6).ToUpperInvariant() : "LOCAL";
    }

    /// <summary>
    /// Embedded zero-configuration LAN / Local-Network Multiplayer Server & UDP Auto-Discovery Radar.
    /// Speaks the exact HTTP (/api/servers) and RFC 6455 WebSocket (/api/rooms/{code}/ws) wire protocol
    /// used by ServerBrowser and RoomClient, so local worlds and self-hosted servers work out of the box
    /// over Wi-Fi, Ethernet, Radmin VPN, Tailscale, ZeroTier, or localhost without installing Node.js.
    /// </summary>
    public sealed class LocalLanServer : IDisposable
    {
        public const int DefaultLanPort = 8787;
        public const int DiscoveryUdpPort = 47777;
        const string WsGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static LocalLanServer Active { get; private set; }

        public int Port { get; private set; }
        public string LocalIp { get; private set; } = "127.0.0.1";
        public string RoomCode { get; private set; } = "";
        public string WorldName { get; private set; } = "Локальный мир";
        public string HostPlayerName { get; private set; } = "Host";
        public int Seed { get; private set; } = 2506;
        public int Mode { get; private set; }
        public int MaxPlayers { get; private set; } = 8;
        public bool IsRunning => running;
        public string ConnectLocalUrl => "http://127.0.0.1:" + Port;
        public string ShareAddress => LocalIp + ":" + Port;
        public string ShortCode => RoomCode.Length >= 6 ? RoomCode.Substring(0, 6).ToUpperInvariant() : "LAN";

#if !UNITY_WEBGL || UNITY_EDITOR
        TcpListener tcpListener;
        UdpClient beaconUdp;
        Thread acceptThread;
        Thread beaconThread;
#endif
        volatile bool running;
        readonly object roomLock = new object();
        readonly Dictionary<string, LanPeer> peers = new Dictionary<string, LanPeer>();
        readonly Dictionary<long, NetBlock> worldEdits = new Dictionary<long, NetBlock>();
        readonly List<SavedBlock> preservedOtherRealmEdits = new List<SavedBlock>();
        readonly Dictionary<string, string> savedPlayers = new Dictionary<string, string>();
        long revision;
        bool roomStarted = true;
        string hostPeerId = "";
        readonly long createdAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        sealed class LanPeer
        {
            public string id;
            public string key;
            public string name;
            public float x, y, z = 4f, yaw;
            public int weapon, offhand;
            public int[] equipment = new int[8];
#if !UNITY_WEBGL || UNITY_EDITOR
            public TcpClient client;
            public Stream stream;
#endif
            public readonly object sendLock = new object();
        }

        public static string DetectLocalIPv4()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            try
            {
                string fallback = null;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address))
                            continue;
                        string s = ua.Address.ToString();
                        if (s.StartsWith("192.168.", StringComparison.Ordinal) || s.StartsWith("10.", StringComparison.Ordinal))
                            return s;
                        if (fallback == null && !s.StartsWith("169.254.", StringComparison.Ordinal))
                            fallback = s;
                    }
                }
                if (!string.IsNullOrEmpty(fallback)) return fallback;
            }
            catch { }
#endif
            return "127.0.0.1";
        }

        static long CellKey(int x, int y, int z)
        {
            return (((long)x & 0x3FFFFFL) << 42) | (((long)y & 0xFFFL) << 30) | ((long)z & 0x3FFFFFFFL);
        }

        public static LocalLanServer StartOrReplace(string worldName, string hostName, int seed, int mode, int maxPlayers = 8, SavedBlock[] preloadEdits = null, int preferredPort = DefaultLanPort)
        {
            StopActive();
            var server = new LocalLanServer();
            if (!server.Start(worldName, hostName, seed, mode, maxPlayers, preloadEdits, preferredPort))
            {
                server.Dispose();
                return null;
            }
            Active = server;
            return server;
        }

        public static void StopActive()
        {
            if (Active != null)
            {
                Active.Dispose();
                Active = null;
            }
        }

        public bool Start(string worldName, string hostName, int seed, int mode, int maxPlayers, SavedBlock[] preloadEdits, int preferredPort)
        {
            WorldName = string.IsNullOrWhiteSpace(worldName) ? "Локальный мир" : worldName.Trim();
            if (WorldName.Length > 32) WorldName = WorldName.Substring(0, 32);
            HostPlayerName = string.IsNullOrWhiteSpace(hostName) ? "Player" : hostName.Trim();
            Seed = seed;
            Mode = Mathf.Clamp(mode, 0, 3);
            MaxPlayers = Mathf.Clamp(maxPlayers, 2, 16);
            RoomCode = Guid.NewGuid().ToString("N");
            LocalIp = DetectLocalIPv4();

            if (preloadEdits != null)
            {
                lock (roomLock)
                {
                    foreach (var e in preloadEdits)
                    {
                        if (e == null) continue;
                        if (e.realm != 0)
                        {
                            preservedOtherRealmEdits.Add(new SavedBlock { realm = e.realm, x = e.x, y = e.y, z = e.z, block = e.block });
                            continue;
                        }
                        revision++;
                        worldEdits[CellKey(e.x, e.y, e.z)] = new NetBlock { x = e.x, y = e.y, z = e.z, block = e.block, rev = revision };
                    }
                }
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            for (int p = preferredPort; p <= preferredPort + 16; p++)
            {
                try
                {
                    var listener = new TcpListener(IPAddress.Any, p);
                    listener.Start();
                    tcpListener = listener;
                    Port = p;
                    break;
                }
                catch
                {
                    tcpListener = null;
                }
            }
            if (tcpListener == null)
            {
                try
                {
                    var listener = new TcpListener(IPAddress.Any, 0);
                    listener.Start();
                    tcpListener = listener;
                    Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                }
                catch
                {
                    return false;
                }
            }

            running = true;
            acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "LC.LocalLanAccept" };
            acceptThread.Start();

            beaconThread = new Thread(BeaconLoop) { IsBackground = true, Name = "LC.LocalLanBeacon" };
            beaconThread.Start();
            return true;
#else
            return false;
#endif
        }

        public int ConnectedPlayerCount
        {
            get { lock (roomLock) return peers.Count; }
        }

        public SavedBlock[] ExportAllSavedBlocks()
        {
            lock (roomLock)
            {
                var list = new List<SavedBlock>(worldEdits.Count + preservedOtherRealmEdits.Count);
                foreach (var kv in worldEdits)
                {
                    var b = kv.Value;
                    list.Add(new SavedBlock { realm = 0, x = b.x, y = b.y, z = b.z, block = b.block });
                }
                foreach (var other in preservedOtherRealmEdits)
                    list.Add(new SavedBlock { realm = other.realm, x = other.x, y = other.y, z = other.z, block = other.block });
                return list.ToArray();
            }
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        void BeaconLoop()
        {
            try
            {
                beaconUdp = new UdpClient();
                beaconUdp.EnableBroadcast = true;
                var broadcastEp = new IPEndPoint(IPAddress.Broadcast, DiscoveryUdpPort);
                var loopbackEp = new IPEndPoint(IPAddress.Loopback, DiscoveryUdpPort);
                while (running)
                {
                    try
                    {
                        string beaconJson = "{\"magic\":\"LC_LAN_V1\",\"title\":\"" + Escape(WorldName) +
                            "\",\"hostName\":\"" + Escape(HostPlayerName) +
                            "\",\"ip\":\"" + Escape(LocalIp) +
                            "\",\"port\":" + Port +
                            ",\"code\":\"" + Escape(RoomCode) +
                            "\",\"mode\":" + Mode +
                            ",\"seed\":" + Seed +
                            ",\"players\":" + Math.Max(1, ConnectedPlayerCount) +
                            ",\"maxPlayers\":" + MaxPlayers + "}";
                        byte[] bytes = Encoding.UTF8.GetBytes(beaconJson);
                        try { beaconUdp.Send(bytes, bytes.Length, broadcastEp); } catch { }
                        try { beaconUdp.Send(bytes, bytes.Length, loopbackEp); } catch { }
                    }
                    catch { }
                    Thread.Sleep(1400);
                }
            }
            catch { }
        }

        void AcceptLoop()
        {
            while (running && tcpListener != null)
            {
                try
                {
                    var client = tcpListener.AcceptTcpClient();
                    client.NoDelay = true;
                    var worker = new Thread(() => HandleClient(client)) { IsBackground = true, Name = "LC.LanPeer" };
                    worker.Start();
                }
                catch
                {
                    if (!running) break;
                }
            }
        }

        void HandleClient(TcpClient tcp)
        {
            try
            {
                var stream = tcp.GetStream();
                stream.ReadTimeout = 15000;
                string requestLine;
                var headers = ReadHttpHeaders(stream, out requestLine);
                if (string.IsNullOrEmpty(requestLine)) { tcp.Close(); return; }

                string[] parts = requestLine.Split(' ');
                if (parts.Length < 2) { tcp.Close(); return; }
                string method = parts[0].ToUpperInvariant();
                string rawTarget = parts[1];
                string path = rawTarget;
                string query = "";
                int qIdx = rawTarget.IndexOf('?');
                if (qIdx >= 0) { path = rawTarget.Substring(0, qIdx); query = rawTarget.Substring(qIdx + 1); }

                if (method == "OPTIONS")
                {
                    WriteHttpJson(stream, 200, "{}");
                    tcp.Close();
                    return;
                }

                if (path == "/api/servers" && method == "GET")
                {
                    int curPlayers;
                    lock (roomLock) curPlayers = peers.Count;
                    string serverJson = "{\"type\":\"servers\",\"servers\":[{\"code\":\"" + Escape(RoomCode) +
                        "\",\"name\":\"" + Escape(WorldName) +
                        "\",\"mode\":" + Mode +
                        ",\"seed\":" + Seed +
                        ",\"dimension\":0,\"rulesVersion\":" + GameRules.Version +
                        ",\"contentVersion\":11,\"players\":" + curPlayers +
                        ",\"maxPlayers\":" + MaxPlayers +
                        ",\"createdAt\":" + createdAtMs +
                        ",\"started\":true,\"offline\":false}]}";
                    WriteHttpJson(stream, 200, serverJson);
                    tcp.Close();
                    return;
                }

                if (path == "/api/servers" && method == "POST")
                {
                    int contentLen = 0;
                    if (headers.TryGetValue("content-length", out string cl)) int.TryParse(cl, out contentLen);
                    if (contentLen > 0 && contentLen < 16384)
                    {
                        var bodyBytes = new byte[contentLen];
                        ReadExact(stream, bodyBytes, 0, contentLen);
                    }
                    string oneJson = "{\"type\":\"server\",\"code\":\"" + Escape(RoomCode) +
                        "\",\"server\":{\"code\":\"" + Escape(RoomCode) +
                        "\",\"name\":\"" + Escape(WorldName) +
                        "\",\"mode\":" + Mode +
                        ",\"seed\":" + Seed +
                        ",\"dimension\":0,\"rulesVersion\":" + GameRules.Version +
                        ",\"contentVersion\":11,\"players\":0,\"maxPlayers\":" + MaxPlayers +
                        ",\"createdAt\":" + createdAtMs + ",\"started\":true,\"offline\":false}}";
                    WriteHttpJson(stream, 200, oneJson);
                    tcp.Close();
                    return;
                }

                if (path.StartsWith("/api/rooms/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/ws", StringComparison.OrdinalIgnoreCase))
                {
                    if (!headers.TryGetValue("sec-websocket-key", out string wsKey) || string.IsNullOrEmpty(wsKey))
                    {
                        WriteHttpJson(stream, 400, "{\"ok\":false,\"message\":\"Expected WebSocket upgrade\"}");
                        tcp.Close();
                        return;
                    }
                    stream.ReadTimeout = Timeout.Infinite;
                    UpgradeWebSocket(tcp, stream, wsKey, query);
                    return;
                }

                WriteHttpJson(stream, 404, "{\"ok\":false,\"message\":\"Not found\"}");
                tcp.Close();
            }
            catch
            {
                try { tcp.Close(); } catch { }
            }
        }

        void UpgradeWebSocket(TcpClient tcp, Stream stream, string wsKey, string query)
        {
            string accept;
            using (var sha1 = SHA1.Create())
                accept = Convert.ToBase64String(sha1.ComputeHash(Encoding.ASCII.GetBytes(wsKey.Trim() + WsGuid)));

            string handshake =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
            byte[] hsBytes = Encoding.ASCII.GetBytes(handshake);
            stream.Write(hsBytes, 0, hsBytes.Length);
            stream.Flush();

            var q = ParseQuery(query);
            string playerName = q.TryGetValue("name", out string nm) && !string.IsNullOrWhiteSpace(nm) ? nm.Trim() : "Игрок";
            if (playerName.Length > 24) playerName = playerName.Substring(0, 24);
            string playerKey = q.TryGetValue("key", out string pk) && !string.IsNullOrWhiteSpace(pk) ? pk.Trim() : Guid.NewGuid().ToString("N");
            string peerId = "p_" + Guid.NewGuid().ToString("N").Substring(0, 10);

            var peer = new LanPeer
            {
                id = peerId,
                key = playerKey,
                name = playerName,
                x = 0.5f,
                y = 66f,
                z = 0.5f,
                client = tcp,
                stream = stream
            };

            string welcomeJson;
            string joinChatJson;
            lock (roomLock)
            {
                if (peers.Count >= MaxPlayers)
                {
                    SendWsText(peer, "{\"type\":\"error\",\"message\":\"Локальный сервер заполнен (" + MaxPlayers + "/" + MaxPlayers + ")\"}");
                    tcp.Close();
                    return;
                }
                if (string.IsNullOrEmpty(hostPeerId)) hostPeerId = peerId;
                peers[peerId] = peer;

                savedPlayers.TryGetValue(playerKey, out string savedRaw);
                welcomeJson = FormatWelcomeLocked(peerId, savedRaw);
                joinChatJson = "{\"type\":\"chat\",\"name\":\"LAN\",\"message\":\"" +
                    Escape(playerName + " подключился к локальному миру (" + peers.Count + "/" + MaxPlayers + ")") + "\"}";
            }

            SendWsText(peer, welcomeJson);
            BroadcastJson(joinChatJson, null);

            try
            {
                while (running && tcp.Connected)
                {
                    string json = ReadWsFrame(stream, out int opcode);
                    if (opcode == 8 || json == null) break;
                    if (opcode == 9) { SendWsControl(peer, 10); continue; }
                    if (opcode != 1 || json.Length == 0) continue;
                    HandlePacket(peer, json);
                }
            }
            catch { }
            finally
            {
                string leaveJson = null;
                string leaveChatJson = null;
                lock (roomLock)
                {
                    if (peers.Remove(peer.id))
                    {
                        if (hostPeerId == peer.id)
                        {
                            hostPeerId = "";
                            foreach (var k in peers.Keys) { hostPeerId = k; break; }
                        }
                        leaveJson = "{\"type\":\"leave\",\"id\":\"" + Escape(peer.id) + "\"}";
                        leaveChatJson = "{\"type\":\"chat\",\"name\":\"LAN\",\"message\":\"" + Escape(peer.name + " вышел из мира") + "\"}";
                    }
                }
                if (leaveJson != null) BroadcastJson(leaveJson, peer.id);
                if (leaveChatJson != null) BroadcastJson(leaveChatJson, peer.id);
                try { tcp.Close(); } catch { }
            }
        }

        void HandlePacket(LanPeer sender, string json)
        {
            string pktType = ExtractString(json, "type");
            if (string.IsNullOrEmpty(pktType)) return;

            switch (pktType)
            {
                case "lobby_start":
                {
                    string startJson;
                    lock (roomLock)
                    {
                        roomStarted = true;
                        startJson = "{\"type\":\"lobby_start\",\"hostId\":\"" + Escape(hostPeerId) +
                            "\",\"players\":" + FormatPlayersLocked() +
                            ",\"clock\":460,\"serverNow\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + "}";
                    }
                    BroadcastJson(startJson, null);
                    break;
                }
                case "position":
                {
                    float px = ExtractFloat(json, "x", sender.x);
                    float py = ExtractFloat(json, "y", sender.y);
                    float pz = ExtractFloat(json, "z", sender.z);
                    float pyaw = ExtractFloat(json, "yaw", sender.yaw);
                    int pweapon = ExtractInt(json, "weapon", sender.weapon);
                    int poffhand = ExtractInt(json, "offhand", sender.offhand);
                    int[] peq = ExtractIntArray(json, "equipment");

                    string moveJson;
                    lock (roomLock)
                    {
                        sender.x = px;
                        sender.y = py;
                        sender.z = pz;
                        sender.yaw = pyaw;
                        sender.weapon = pweapon;
                        sender.offhand = poffhand;
                        if (peq != null && peq.Length == 8) sender.equipment = peq;
                        moveJson = FormatSinglePlayerJson(sender, "player");
                    }
                    BroadcastJson(moveJson, sender.id);
                    break;
                }
                case "edit":
                {
                    int bx = Mathf.RoundToInt(ExtractFloat(json, "x", 0f));
                    int by = Mathf.RoundToInt(ExtractFloat(json, "y", -1f));
                    int bz = Mathf.RoundToInt(ExtractFloat(json, "z", 0f));
                    int bId = ExtractInt(json, "block", 0);
                    if (by <= 0 || by >= 256 || bId < 0 || bId > 97) return;

                    string editJson;
                    lock (roomLock)
                    {
                        revision++;
                        worldEdits[CellKey(bx, by, bz)] = new NetBlock { x = bx, y = by, z = bz, block = bId, rev = revision };
                        editJson = "{\"type\":\"edit\",\"id\":\"" + Escape(sender.id) +
                            "\",\"x\":" + bx + ",\"y\":" + by + ",\"z\":" + bz +
                            ",\"block\":" + bId + ",\"rev\":" + revision + "}";
                    }
                    BroadcastJson(editJson, null);
                    break;
                }
                case "chat":
                {
                    string msg = (ExtractString(json, "message") ?? "").Trim();
                    if (msg.Length == 0) return;
                    if (msg.Length > 240) msg = msg.Substring(0, 240);
                    string chatJson = "{\"type\":\"chat\",\"id\":\"" + Escape(sender.id) +
                        "\",\"name\":\"" + Escape(sender.name) +
                        "\",\"message\":\"" + Escape(msg) + "\"}";
                    BroadcastJson(chatJson, null);
                    break;
                }
                case "state":
                {
                    string rawSave = ExtractRawObject(json, "save");
                    lock (roomLock)
                    {
                        if (!string.IsNullOrEmpty(rawSave) && !string.IsNullOrEmpty(sender.key))
                            savedPlayers[sender.key] = rawSave;
                    }
                    SendWsText(sender, "{\"type\":\"state_saved\",\"id\":\"" + Escape(sender.id) + "\"}");
                    break;
                }
            }
        }

        string FormatWelcomeLocked(string peerId, string savedRaw)
        {
            var sb = new StringBuilder(1024 + worldEdits.Count * 48);
            sb.Append("{\"type\":\"welcome\",\"id\":\"").Append(Escape(peerId))
              .Append("\",\"host\":").Append(peerId == hostPeerId ? "true" : "false")
              .Append(",\"hostId\":\"").Append(Escape(hostPeerId))
              .Append("\",\"lobby\":false,\"started\":").Append(roomStarted ? "true" : "false")
              .Append(",\"version\":1,\"rulesVersion\":").Append(GameRules.Version)
              .Append(",\"serverRulesVersion\":").Append(GameRules.Version)
              .Append(",\"contentVersion\":11,\"seed\":").Append(Seed)
              .Append(",\"dimension\":0,\"mode\":").Append(Mode)
              .Append(",\"maxPlayers\":").Append(MaxPlayers)
              .Append(",\"clock\":460,\"serverNow\":").Append(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
              .Append(",\"rev\":").Append(revision)
              .Append(",\"edits\":[");

            bool first = true;
            foreach (var kv in worldEdits)
            {
                if (!first) sb.Append(',');
                first = false;
                var b = kv.Value;
                sb.Append("{\"x\":").Append(b.x).Append(",\"y\":").Append(b.y).Append(",\"z\":").Append(b.z)
                  .Append(",\"block\":").Append(b.block).Append(",\"rev\":").Append(b.rev).Append('}');
            }
            sb.Append("],\"players\":").Append(FormatPlayersLocked());
            if (!string.IsNullOrEmpty(savedRaw))
                sb.Append(",\"save\":").Append(savedRaw);
            sb.Append('}');
            return sb.ToString();
        }

        string FormatPlayersLocked()
        {
            var sb = new StringBuilder(256);
            sb.Append('[');
            bool first = true;
            foreach (var p in peers.Values)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(FormatSinglePlayerJson(p, null));
            }
            sb.Append(']');
            return sb.ToString();
        }

        static string FormatSinglePlayerJson(LanPeer p, string withType)
        {
            var sb = new StringBuilder(192);
            sb.Append('{');
            if (!string.IsNullOrEmpty(withType))
                sb.Append("\"type\":\"").Append(withType).Append("\",");
            sb.Append("\"id\":\"").Append(Escape(p.id))
              .Append("\",\"name\":\"").Append(Escape(p.name))
              .Append("\",\"x\":").Append(p.x.ToString("0.###", CultureInfo.InvariantCulture))
              .Append(",\"y\":").Append(p.y.ToString("0.###", CultureInfo.InvariantCulture))
              .Append(",\"z\":").Append(p.z.ToString("0.###", CultureInfo.InvariantCulture))
              .Append(",\"yaw\":").Append(p.yaw.ToString("0.##", CultureInfo.InvariantCulture))
              .Append(",\"weapon\":").Append(p.weapon)
              .Append(",\"offhand\":").Append(p.offhand)
              .Append(",\"equipment\":[");
            for (int i = 0; i < 8; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(p.equipment != null && i < p.equipment.Length ? p.equipment[i] : 0);
            }
            sb.Append("]}");
            return sb.ToString();
        }

        void BroadcastJson(string json, string excludeId)
        {
            List<LanPeer> targets;
            lock (roomLock) targets = new List<LanPeer>(peers.Values);
            foreach (var p in targets)
            {
                if (excludeId != null && p.id == excludeId) continue;
                SendWsText(p, json);
            }
        }

        static void SendWsText(LanPeer peer, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            lock (peer.sendLock)
            {
                try
                {
                    var s = peer.stream;
                    if (s == null) return;
                    s.WriteByte(0x81);
                    if (payload.Length < 126)
                    {
                        s.WriteByte((byte)payload.Length);
                    }
                    else if (payload.Length <= 65535)
                    {
                        s.WriteByte(126);
                        s.WriteByte((byte)((payload.Length >> 8) & 0xFF));
                        s.WriteByte((byte)(payload.Length & 0xFF));
                    }
                    else
                    {
                        s.WriteByte(127);
                        long len = payload.Length;
                        for (int i = 7; i >= 0; i--) s.WriteByte((byte)((len >> (i * 8)) & 0xFF));
                    }
                    s.Write(payload, 0, payload.Length);
                    s.Flush();
                }
                catch { }
            }
        }

        static void SendWsControl(LanPeer peer, byte opcode)
        {
            lock (peer.sendLock)
            {
                try
                {
                    peer.stream.WriteByte((byte)(0x80 | (opcode & 0x0F)));
                    peer.stream.WriteByte(0);
                    peer.stream.Flush();
                }
                catch { }
            }
        }

        static string ReadWsFrame(Stream s, out int opcode)
        {
            opcode = 8;
            int b0 = s.ReadByte();
            if (b0 < 0) return null;
            int b1 = s.ReadByte();
            if (b1 < 0) return null;
            opcode = b0 & 0x0F;
            bool masked = (b1 & 0x80) != 0;
            long len = b1 & 0x7F;
            if (len == 126)
            {
                var ext = new byte[2];
                if (!ReadExact(s, ext, 0, 2)) return null;
                len = (ext[0] << 8) | ext[1];
            }
            else if (len == 127)
            {
                var ext = new byte[8];
                if (!ReadExact(s, ext, 0, 8)) return null;
                len = 0;
                for (int i = 0; i < 8; i++) len = (len << 8) | ext[i];
            }
            if (len < 0 || len > 1_500_000) return null;
            byte[] mask = null;
            if (masked)
            {
                mask = new byte[4];
                if (!ReadExact(s, mask, 0, 4)) return null;
            }
            var data = new byte[(int)len];
            if (len > 0 && !ReadExact(s, data, 0, (int)len)) return null;
            if (masked)
                for (int i = 0; i < data.Length; i++) data[i] ^= mask[i & 3];
            return opcode == 1 ? Encoding.UTF8.GetString(data) : "";
        }

        static Dictionary<string, string> ReadHttpHeaders(Stream s, out string requestLine)
        {
            requestLine = ReadAsciiLine(s);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(requestLine)) return headers;
            for (int i = 0; i < 64; i++)
            {
                string line = ReadAsciiLine(s);
                if (string.IsNullOrEmpty(line)) break;
                int colon = line.IndexOf(':');
                if (colon > 0) headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }
            return headers;
        }

        static string ReadAsciiLine(Stream s)
        {
            var sb = new StringBuilder(128);
            while (sb.Length < 4096)
            {
                int b = s.ReadByte();
                if (b < 0) return sb.Length > 0 ? sb.ToString() : null;
                if (b == '\r') continue;
                if (b == '\n') return sb.ToString();
                sb.Append((char)b);
            }
            return sb.ToString();
        }

        static bool ReadExact(Stream s, byte[] buf, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = s.Read(buf, offset + total, count - total);
                if (n <= 0) return false;
                total += n;
            }
            return true;
        }

        static void WriteHttpJson(Stream s, int code, string body)
        {
            byte[] data = Encoding.UTF8.GetBytes(body);
            string status = code == 200 ? "200 OK" : code == 400 ? "400 Bad Request" : "404 Not Found";
            string head =
                "HTTP/1.1 " + status + "\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, DELETE, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type, Authorization\r\n" +
                "Content-Length: " + data.Length + "\r\n" +
                "Connection: close\r\n\r\n";
            byte[] hb = Encoding.ASCII.GetBytes(head);
            s.Write(hb, 0, hb.Length);
            s.Write(data, 0, data.Length);
            s.Flush();
        }

        static Dictionary<string, string> ParseQuery(string query)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query)) return map;
            foreach (var part in query.Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq > 0)
                    map[Uri.UnescapeDataString(part.Substring(0, eq))] = Uri.UnescapeDataString(part.Substring(eq + 1));
            }
            return map;
        }

        static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }
#endif

        internal static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string token = "\"" + key + "\"";
            int idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + token.Length);
            if (colon < 0) return null;
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return null;
            var sb = new StringBuilder();
            for (int i = q1 + 1; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '\\' && i + 1 < json.Length)
                {
                    char next = json[++i];
                    if (next == 'n') sb.Append('\n');
                    else sb.Append(next);
                    continue;
                }
                if (c == '"') return sb.ToString();
                sb.Append(c);
            }
            return null;
        }

        internal static float ExtractFloat(string json, string key, float fallback)
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            string token = "\"" + key + "\"";
            int idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return fallback;
            int colon = json.IndexOf(':', idx + token.Length);
            if (colon < 0) return fallback;
            int p = colon + 1;
            while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
            int start = p;
            while (p < json.Length && (char.IsDigit(json[p]) || json[p] == '-' || json[p] == '+' || json[p] == '.' || json[p] == 'e' || json[p] == 'E'))
                p++;
            if (p <= start) return fallback;
            return float.TryParse(json.Substring(start, p - start), NumberStyles.Float, CultureInfo.InvariantCulture, out float val) ? val : fallback;
        }

        internal static int ExtractInt(string json, string key, int fallback)
        {
            return Mathf.RoundToInt(ExtractFloat(json, key, fallback));
        }

        internal static int[] ExtractIntArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string token = "\"" + key + "\"";
            int idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return null;
            int b1 = json.IndexOf('[', idx + token.Length);
            if (b1 < 0) return null;
            int b2 = json.IndexOf(']', b1 + 1);
            if (b2 < 0) return null;
            string inner = json.Substring(b1 + 1, b2 - b1 - 1).Trim();
            if (inner.Length == 0) return Array.Empty<int>();
            string[] parts = inner.Split(',');
            var res = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out res[i]);
            return res;
        }

        internal static string ExtractRawObject(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string token = "\"" + key + "\"";
            int idx = json.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0) return null;
            int colon = json.IndexOf(':', idx + token.Length);
            if (colon < 0) return null;
            int start = json.IndexOf('{', colon + 1);
            if (start < 0) return null;
            int depth = 0;
            bool inStr = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (inStr)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') inStr = true;
                else if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return json.Substring(start, i - start + 1);
                }
            }
            return null;
        }

        internal static LanBeaconInfo ParseBeaconJson(string json)
        {
            string magic = ExtractString(json, "magic");
            if (magic != "LC_LAN_V1") return null;
            return new LanBeaconInfo
            {
                magic = magic,
                title = ExtractString(json, "title") ?? "Локальный мир",
                hostName = ExtractString(json, "hostName") ?? "Player",
                ip = ExtractString(json, "ip") ?? "127.0.0.1",
                port = ExtractInt(json, "port", 0),
                code = ExtractString(json, "code") ?? "",
                mode = ExtractInt(json, "mode", 0),
                seed = ExtractInt(json, "seed", 2506),
                players = ExtractInt(json, "players", 1),
                maxPlayers = ExtractInt(json, "maxPlayers", 8)
            };
        }

        public void Dispose()
        {
            running = false;
#if !UNITY_WEBGL || UNITY_EDITOR
            try { tcpListener?.Stop(); } catch { }
            try { beaconUdp?.Close(); } catch { }
            lock (roomLock)
            {
                foreach (var p in peers.Values)
                {
                    try { p.client?.Close(); } catch { }
                }
                peers.Clear();
            }
#endif
        }
    }

    /// <summary>Passive UDP LAN listener that discovers LocalLanServer worlds on the local network.</summary>
    public static class LanDiscoveryRadar
    {
        static readonly ConcurrentDictionary<string, LanBeaconInfo> Discovered = new ConcurrentDictionary<string, LanBeaconInfo>();
#if !UNITY_WEBGL || UNITY_EDITOR
        static UdpClient listener;
        static Thread listenThread;
        static volatile bool listening;
#endif

        public static void EnsureListening()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (listening) return;
            try
            {
                var udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, LocalLanServer.DiscoveryUdpPort));
                listener = udp;
                listening = true;
                listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "LC.LanRadar" };
                listenThread.Start();
            }
            catch
            {
                listening = false;
            }
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        static void ListenLoop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (listening && listener != null)
            {
                try
                {
                    byte[] bytes = listener.Receive(ref ep);
                    string json = Encoding.UTF8.GetString(bytes);
                    var info = LocalLanServer.ParseBeaconJson(json);
                    if (info == null || info.magic != "LC_LAN_V1" || info.port <= 0 || string.IsNullOrEmpty(info.code))
                        continue;
                    if (string.IsNullOrEmpty(info.ip) || info.ip == "0.0.0.0" || info.ip == "127.0.0.1")
                        info.ip = ep.Address.ToString();
                    info.lastSeenRealtime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                    Discovered[info.ip + ":" + info.port] = info;
                }
                catch
                {
                    if (!listening) break;
                }
            }
        }
#endif

        public static LanBeaconInfo[] ActiveWorlds()
        {
            EnsureListening();
            double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            var list = new List<LanBeaconInfo>();

            // Always surface our own running LocalLanServer immediately without waiting for UDP
            var self = LocalLanServer.Active;
            if (self != null && self.IsRunning)
            {
                list.Add(new LanBeaconInfo
                {
                    title = self.WorldName + " (ваш ПК)",
                    hostName = self.HostPlayerName,
                    ip = self.LocalIp,
                    port = self.Port,
                    code = self.RoomCode,
                    mode = self.Mode,
                    seed = self.Seed,
                    players = Math.Max(1, self.ConnectedPlayerCount),
                    maxPlayers = self.MaxPlayers,
                    lastSeenRealtime = now
                });
            }

            foreach (var kv in Discovered)
            {
                if (now - kv.Value.lastSeenRealtime > 6.5)
                {
                    Discovered.TryRemove(kv.Key, out _);
                    continue;
                }
                if (self != null && self.IsRunning && kv.Value.code == self.RoomCode)
                    continue;
                list.Add(kv.Value);
            }
            return list.ToArray();
        }
    }
}
