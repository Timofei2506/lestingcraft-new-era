using System;
using System.Collections.Generic;
using System.Text;

namespace LestingCraft.Networking
{
    /// <summary>
    /// One server the player added by address, exactly like the Minecraft server
    /// list: a name, an IP or domain, a port and the last time it answered.
    /// Pure C# on purpose so the parsing rules can be tested without Unity.
    /// </summary>
    [Serializable]
    public sealed class ServerAddress
    {
        public string name = "";
        public string host = "";
        public int port = ServerAddressRules.DefaultPort;
        public string note = "";
        public long lastSeen;
        public int lastPlayers = -1;

        /// <summary>What the player sees: "mc.example.com" or "192.168.1.7:8787".</summary>
        public string Address { get { return ServerAddressRules.Display(host, port); } }

        /// <summary>Base URL used by HTTP calls and by the websocket connection.</summary>
        public string Url { get { return ServerAddressRules.Url(host, port); } }

        public string Title { get { return string.IsNullOrEmpty(name) ? host : name; } }

        public ServerAddress Copy() { return (ServerAddress)MemberwiseClone(); }
    }

    /// <summary>Address parsing and formatting. Never throws on user input.</summary>
    public static class ServerAddressRules
    {
        public const int DefaultPort = 443;
        public const int MinPort = 1;
        public const int MaxPort = 65535;
        public const int MaxHostLength = 253;

        static bool Allowed(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                || c == '.' || c == '-' || c == '_' || c == ':' || c == '%';
        }

        /// <summary>
        /// Accepts "example.com", "example.com:8787", "http://127.0.0.1:8787/",
        /// "https://host/", "[::1]:8787". The address may carry the port itself;
        /// otherwise the separate port field (or the scheme default) is used.
        /// </summary>
        public static bool TryParse(string input, string portText, out string host, out int port, out string error)
        {
            host = string.Empty; port = DefaultPort; error = string.Empty;
            string raw = (input ?? string.Empty).Trim();
            if (raw.Length == 0) { error = "Введите адрес сервера"; return false; }
            string scheme = string.Empty;
            int schemeEnd = raw.IndexOf("://", StringComparison.Ordinal);
            if (schemeEnd > 0)
            {
                scheme = raw.Substring(0, schemeEnd).ToLowerInvariant();
                raw = raw.Substring(schemeEnd + 3);
                if (scheme != "http" && scheme != "https" && scheme != "ws" && scheme != "wss")
                { error = "Адрес должен начинаться с http:// или https://"; return false; }
                if (scheme == "ws") scheme = "http";
                if (scheme == "wss") scheme = "https";
            }
            int tail = raw.IndexOfAny(new[] { '/', '?', '#' });
            if (tail >= 0) raw = raw.Substring(0, tail);
            if (raw.Length == 0) { error = "Введите адрес сервера"; return false; }
            int explicitPort = -1;
            if (raw[0] == '[')
            {
                int close = raw.IndexOf(']');
                if (close < 0) { error = "Некорректный IPv6-адрес"; return false; }
                string rest = raw.Substring(close + 1);
                if (rest.Length > 0)
                {
                    if (rest[0] != ':' || !int.TryParse(rest.Substring(1), out explicitPort))
                    { error = "Порт должен быть числом"; return false; }
                }
                raw = raw.Substring(1, close - 1);
            }
            else
            {
                int colon = raw.LastIndexOf(':');
                if (colon >= 0)
                {
                    if (!int.TryParse(raw.Substring(colon + 1), out explicitPort)) { error = "Порт должен быть числом"; return false; }
                    raw = raw.Substring(0, colon);
                }
            }
            host = raw.Trim().ToLowerInvariant();
            if (host.Length == 0) { error = "Введите адрес сервера"; return false; }
            if (host.Length > MaxHostLength) { error = "Адрес слишком длинный"; return false; }
            host = host.TrimEnd('.');
            if (host.Length == 0) { error = "Введите адрес сервера"; return false; }
            if (host[0] == '.' ) { error = "Адрес не может начинаться с точки"; return false; }
            for (int i = 0; i < host.Length; i++)
                if (!Allowed(host[i])) { error = "Адрес содержит недопустимые символы"; return false; }
            if (explicitPort >= 0) port = explicitPort;
            else
            {
                string trimmed = (portText ?? string.Empty).Trim();
                if (trimmed.Length > 0)
                {
                    if (!int.TryParse(trimmed, out port)) { error = "Порт должен быть числом"; return false; }
                }
                else port = scheme == "http" ? 80 : DefaultPort;
            }
            if (port < MinPort || port > MaxPort) { error = "Порт должен быть от 1 до 65535"; return false; }
            return true;
        }

        public static bool IsSecure(int port) { return port == 443; }

        /// <summary>"https://host" on 443, otherwise "http://host:port".</summary>
        public static string Url(string host, int port)
        {
            string clean = string.IsNullOrEmpty(host) ? "127.0.0.1" : host;
            if (clean.IndexOf(':') >= 0 && clean[0] != '[') clean = "[" + clean + "]";
            return (IsSecure(port) ? "https://" : "http://") + clean + (IsSecure(port) ? string.Empty : ":" + port);
        }

        /// <summary>Short label: the port is hidden when it is the default one.</summary>
        public static string Display(string host, int port)
        {
            string clean = host ?? string.Empty;
            if (clean.IndexOf(':') >= 0 && (clean.Length == 0 || clean[0] != '[')) clean = "[" + clean + "]";
            return IsSecure(port) ? clean : clean + ":" + port;
        }

        /// <summary>Default display name for a freshly added address.</summary>
        public static string SuggestName(string host)
        {
            if (string.IsNullOrEmpty(host)) return "Сервер";
            string name = host;
            int dot = name.IndexOf('.');
            if (dot > 0) name = name.Substring(0, dot);
            if (name.Length == 0) name = host;
            return char.ToUpperInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : string.Empty);
        }
    }

    /// <summary>The saved address book, persisted by the game in player prefs.</summary>
    public sealed class ServerBook
    {
        public const int Capacity = 24;
        public readonly List<ServerAddress> Servers = new List<ServerAddress>();

        public int Count { get { return Servers.Count; } }

        public int IndexOf(string host, int port)
        {
            for (int i = 0; i < Servers.Count; i++)
                if (string.Equals(Servers[i].host, host, StringComparison.OrdinalIgnoreCase) && Servers[i].port == port) return i;
            return -1;
        }

        /// <summary>Adds or updates the address; returns its index, or -1 when full.</summary>
        public int Add(ServerAddress entry)
        {
            if (entry == null) return -1;
            int existing = IndexOf(entry.host, entry.port);
            if (existing >= 0)
            {
                var current = Servers[existing];
                if (!string.IsNullOrEmpty(entry.name)) current.name = entry.name;
                if (!string.IsNullOrEmpty(entry.note)) current.note = entry.note;
                current.lastSeen = entry.lastSeen;
                current.lastPlayers = entry.lastPlayers;
                return existing;
            }
            if (Servers.Count >= Capacity) return -1;
            Servers.Add(entry);
            return Servers.Count - 1;
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= Servers.Count) return false;
            Servers.RemoveAt(index);
            return true;
        }

        public void MarkSeen(int index, int players, long stamp)
        {
            if (index < 0 || index >= Servers.Count) return;
            Servers[index].lastSeen = stamp;
            Servers[index].lastPlayers = players;
        }

        public string ToJson()
        {
            var text = new StringBuilder();
            text.Append("{\"servers\":[");
            for (int i = 0; i < Servers.Count; i++)
            {
                var s = Servers[i];
                if (i > 0) text.Append(',');
                text.Append('{');
                text.Append("\"name\":\"").Append(Escape(s.name)).Append("\",");
                text.Append("\"host\":\"").Append(Escape(s.host)).Append("\",");
                text.Append("\"port\":").Append(s.port).Append(',');
                text.Append("\"note\":\"").Append(Escape(s.note)).Append("\",");
                text.Append("\"lastSeen\":").Append(s.lastSeen).Append(',');
                text.Append("\"lastPlayers\":").Append(s.lastPlayers);
                text.Append('}');
            }
            text.Append("]}");
            return text.ToString();
        }

        public static ServerBook FromJson(string json)
        {
            var book = new ServerBook();
            if (string.IsNullOrEmpty(json)) return book;
            int start = json.IndexOf('[');
            int end = json.LastIndexOf(']');
            if (start < 0 || end <= start) return book;
            string body = json.Substring(start + 1, end - start - 1);
            int cursor = 0;
            while (cursor < body.Length && book.Servers.Count < Capacity)
            {
                int open = body.IndexOf('{', cursor);
                if (open < 0) break;
                int close = body.IndexOf('}', open);
                if (close < 0) break;
                string chunk = body.Substring(open + 1, close - open - 1);
                cursor = close + 1;
                var entry = new ServerAddress();
                entry.name = ReadString(chunk, "name");
                entry.host = ReadString(chunk, "host");
                entry.port = ReadInt(chunk, "port", ServerAddressRules.DefaultPort);
                entry.note = ReadString(chunk, "note");
                entry.lastSeen = ReadLong(chunk, "lastSeen", 0);
                entry.lastPlayers = ReadInt(chunk, "lastPlayers", -1);
                if (entry.host.Length == 0) continue;
                if (entry.port < ServerAddressRules.MinPort || entry.port > ServerAddressRules.MaxPort) entry.port = ServerAddressRules.DefaultPort;
                if (book.IndexOf(entry.host, entry.port) >= 0) continue;
                book.Servers.Add(entry);
            }
            return book;
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var text = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\') text.Append('\\').Append(c);
                else if (c == '\n') text.Append("\\n");
                else if (c == '\r') { }
                else if (c == '\t') text.Append(' ');
                else if (c < ' ') { }
                else text.Append(c);
            }
            return text.ToString();
        }

        static string ReadString(string body, string key)
        {
            string marker = "\"" + key + "\":\"";
            int at = body.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) return string.Empty;
            at += marker.Length;
            var text = new StringBuilder();
            for (int i = at; i < body.Length; i++)
            {
                char c = body[i];
                if (c == '\\' && i + 1 < body.Length)
                {
                    char next = body[++i];
                    text.Append(next == 'n' ? '\n' : next);
                    continue;
                }
                if (c == '"') break;
                text.Append(c);
            }
            return text.ToString();
        }

        static long ReadLong(string body, string key, long fallback)
        {
            string marker = "\"" + key + "\":";
            int at = body.IndexOf(marker, StringComparison.Ordinal);
            if (at < 0) return fallback;
            at += marker.Length;
            int end = at;
            while (end < body.Length && (char.IsDigit(body[end]) || body[end] == '-' || body[end] == '+')) end++;
            long value;
            return long.TryParse(body.Substring(at, end - at), out value) ? value : fallback;
        }

        static int ReadInt(string body, string key, int fallback)
        {
            return (int)ReadLong(body, key, fallback);
        }
    }
}
