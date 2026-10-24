// Compile-time and behaviour check for the multiplayer address book.
// Runs with: mcs -langversion:7.2 ServerBook.cs check_multiplayer_core.cs && mono check_multiplayer_core.exe
using System;
using System.Collections.Generic;
using LestingCraft.Networking;

static class CheckMultiplayerCore
{
    static int failures;

    static void Expect(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.WriteLine("FAIL: " + message);
    }

    static int Main()
    {
        Addresses();
        Book();
        Json();
        Console.WriteLine(failures == 0 ? "PASS multiplayer address book" : failures + " failures");
        return failures == 0 ? 0 : 1;
    }

    static void Addresses()
    {
        string host, error; int port, n = 0;

        Expect(ServerAddressRules.TryParse("example.com", "", out host, out port, out error), "bare domain parses");
        Expect(host == "example.com" && port == 443, "bare domain defaults to 443 https (" + host + ":" + port + ")"); n++;

        Expect(ServerAddressRules.TryParse("https://Play.Example.com/", "", out host, out port, out error), "full URL parses");
        Expect(host == "play.example.com" && port == 443, "url is lowercased and stripped"); n++;

        Expect(ServerAddressRules.TryParse("http://127.0.0.1:8787/api", "", out host, out port, out error), "custom port in URL parses");
        Expect(host == "127.0.0.1" && port == 8787, "custom port wins over the scheme default"); n++;

        Expect(ServerAddressRules.TryParse("192.168.1.7", "8787", out host, out port, out error), "separate port field works");
        Expect(host == "192.168.1.7" && port == 8787, "port field applied when the address has none"); n++;

        Expect(ServerAddressRules.TryParse("host.name:9999", "8787", out host, out port, out error), "explicit port wins");
        Expect(port == 9999, "address port overrides the port field"); n++;

        Expect(!ServerAddressRules.TryParse("", "", out host, out port, out error), "empty address rejected");
        Expect(error.Length > 0, "empty address explains itself"); n++;

        Expect(!ServerAddressRules.TryParse("ftp://example.com", "", out host, out port, out error), "unknown scheme rejected"); n++;
        Expect(!ServerAddressRules.TryParse("example.com:http", "", out host, out port, out error), "non numeric port rejected"); n++;
        Expect(!ServerAddressRules.TryParse("example.com", "0", out host, out port, out error), "port 0 rejected"); n++;
        Expect(!ServerAddressRules.TryParse("example.com", "70000", out host, out port, out error), "port above 65535 rejected"); n++;
        Expect(!ServerAddressRules.TryParse("плохой адрес", "", out host, out port, out error), "cyrillic host rejected"); n++;
        Expect(!ServerAddressRules.TryParse(".hidden", "", out host, out port, out error), "leading dot rejected"); n++;

        Expect(ServerAddressRules.TryParse("[::1]:8787", "", out host, out port, out error), "ipv6 parses");
        Expect(host == "::1" && port == 8787, "ipv6 host and port split"); n++;
        Expect(ServerAddressRules.Display("::1", 8787) == "[::1]:8787", "ipv6 display is bracketed"); n++;

        Expect(ServerAddressRules.Url("example.com", 443) == "https://example.com", "443 renders https");
        Expect(ServerAddressRules.Url("example.com", 8787) == "http://example.com:8787", "custom port renders http");
        Expect(ServerAddressRules.Display("example.com", 443) == "example.com", "default port hidden");
        Expect(ServerAddressRules.Display("example.com", 8787) == "example.com:8787", "custom port shown"); n++;

        Expect(ServerAddressRules.SuggestName("play.example.com") == "Play", "suggested name from domain");
        Expect(ServerAddressRules.SuggestName("") == "Сервер", "suggested name fallback"); n++;

        var entry = new ServerAddress { host = "example.com", port = 8787, name = "Друзья" };
        Expect(entry.Address == "example.com:8787" && entry.Url == "http://example.com:8787" && entry.Title == "Друзья", "address object helpers");
        var unnamed = new ServerAddress { host = "example.com", port = 8787 };
        Expect(unnamed.Title == "example.com", "unnamed entry falls back to host");
        Console.WriteLine("address cases: " + n);
    }

    static void Book()
    {
        var book = new ServerBook();
        int first = book.Add(new ServerAddress { host = "a.example.com", port = 443, name = "Альфа" });
        int second = book.Add(new ServerAddress { host = "b.example.com", port = 8787, name = "Бета" });
        Expect(first == 0 && second == 1 && book.Count == 2, "two addresses stored");

        int again = book.Add(new ServerAddress { host = "A.Example.com", port = 443, name = "Альфа 2" });
        Expect(again == 0 && book.Count == 2, "addresses are unique per host and port");
        Expect(book.Servers[0].name == "Альфа 2", "re-adding updates the display name");

        Expect(book.IndexOf("b.example.com", 8787) == 1, "lookup finds the second entry");
        Expect(book.IndexOf("b.example.com", 443) == -1, "lookup respects the port");

        book.MarkSeen(1, 3, 1700000000);
        Expect(book.Servers[1].lastPlayers == 3 && book.Servers[1].lastSeen == 1700000000, "last seen recorded");

        Expect(book.RemoveAt(0) && book.Count == 1 && book.Servers[0].host == "b.example.com", "removal keeps order");
        Expect(!book.RemoveAt(5), "removing out of range is safe");

        var full = new ServerBook();
        for (int i = 0; i < ServerBook.Capacity + 3; i++)
            Expect(full.Add(new ServerAddress { host = "s" + i + ".example.com", port = 8787 }) >= 0 || i >= ServerBook.Capacity, "add " + i);
        Expect(full.Count == ServerBook.Capacity, "the book is capped at " + ServerBook.Capacity);
        Expect(full.Add(new ServerAddress { host = "overflow.example.com", port = 8787 }) == -1, "a full book refuses new addresses");
    }

    static void Json()
    {
        var book = new ServerBook();
        book.Add(new ServerAddress { host = "play.example.com", port = 8787, name = "Мир \"кавычки\" \\ слэш", note = "строка\nдве", lastSeen = 42, lastPlayers = 2 });
        book.Add(new ServerAddress { host = "example.com", port = 443, name = "" });
        string json = book.ToJson();
        var restored = ServerBook.FromJson(json);
        Expect(restored.Count == 2, "round trip keeps both entries");
        Expect(restored.Servers[0].name == "Мир \"кавычки\" \\ слэш", "quotes and backslashes survive: " + restored.Servers[0].name);
        Expect(restored.Servers[0].port == 8787 && restored.Servers[0].lastPlayers == 2 && restored.Servers[0].lastSeen == 42, "numbers survive");
        Expect(restored.Servers[1].port == 443, "default port survives");

        var broken = ServerBook.FromJson("{\"servers\":[{\"host\":\"example.com\",\"port\":999999}]}");
        Expect(broken.Count == 1 && broken.Servers[0].port == 443, "an out of range port is repaired");
        Expect(ServerBook.FromJson("").Count == 0, "empty json yields an empty book");
        Expect(ServerBook.FromJson("мусор").Count == 0, "garbage yields an empty book");
        Expect(ServerBook.FromJson("{\"servers\":[{\"port\":1},{\"host\":\"ok.example.com\"}]}").Count == 1, "entries without a host are dropped");
    }
}
