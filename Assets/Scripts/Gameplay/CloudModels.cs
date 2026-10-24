using System;
using System.Text;

namespace LestingCraft.Gameplay
{
    // Wire models for /api/cloud/*. Field names are snake_case on purpose: they
    // are parsed by JsonUtility straight from the Supabase RPC jsonb documents,
    // so a rename here would silently break the shop.
    [Serializable] public sealed class CloudProfile { public string id, display_name, avatar_skin, player_key; }
    [Serializable] public sealed class CloudPayload { public string[] palette, gradient; }
    [Serializable] public sealed class CloudSkin
    {
        public string code, kind, title, description;
        public long price;
        public CloudPayload payload;
    }
    [Serializable] public sealed class CloudPack
    {
        public string code, currency, title;
        public long coins, bonus, price_minor;
        public string PriceText { get { return CloudFormat.Price(price_minor, currency); } }
        public string CoinsText { get { return CloudFormat.Coins(coins + bonus); } }
    }
    [Serializable] public sealed class CloudListing
    {
        public string id, item, title, kind, seller, created_at;
        public long price;
        public bool mine;
    }
    [Serializable] public sealed class CloudLedgerRow
    {
        public long delta, balance_after;
        public string reason;
    }
    [Serializable] public sealed class CloudState
    {
        public bool ok;
        public string message;
        public CloudProfile profile;
        public long balance;
        public CloudSkin[] owned, store;
        public CloudPack[] packs;
        public CloudListing[] listings;
        public CloudLedgerRow[] ledger;
    }
    [Serializable] public sealed class CloudBoard
    {
        public bool ok;
        public string message;
        public CloudListing[] listings, mine;
    }
    [Serializable] public sealed class CloudFriend { public string key, name, skin; public bool online; }
    [Serializable] public sealed class CloudFriendRequest
    {
        public long id;
        public string name, key, message, at;
    }
    [Serializable] public sealed class CloudFriendsReply
    {
        public bool ok;
        public string message;
        public CloudFriend[] friends;
        public CloudFriendRequest[] incoming, outgoing;
    }
    [Serializable] public sealed class CloudReply { public bool ok; public string message, item, equipped, result; public long balance; }

    /// <summary>Display helpers shared by the shop, the market and the friend list.</summary>
    public static class CloudFormat
    {
        /// <summary>1234 → "1 234". Coins are never negative in the UI.</summary>
        public static string Coins(long value)
        {
            bool negative = value < 0;
            long left = negative ? -value : value;
            var text = new StringBuilder();
            string digits = left.ToString();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) text.Append(' ');
                text.Append(digits[i]);
            }
            return negative ? "−" + text : text.ToString();
        }

        /// <summary>199 USD cents → "$1.99". Unknown currencies fall back to the code.</summary>
        public static string Price(long minor, string currency)
        {
            string code = string.IsNullOrEmpty(currency) ? "USD" : currency.ToUpperInvariant();
            string symbol = code == "USD" ? "$" : code == "EUR" ? "€" : code == "RUB" ? "₽" : code + " ";
            long units = minor / 100, cents = minor % 100;
            return symbol + units + "." + (cents < 10 ? "0" : string.Empty) + cents;
        }

        public static string Reason(string reason)
        {
            switch (reason)
            {
                case "topup": return "Покупка монет";
                case "purchase": return "Покупка в магазине";
                case "market_sale": return "Продажа лота";
                case "market_purchase": return "Покупка лота";
                case "refund": return "Возврат";
                case "admin": return "Начисление";
                default: return "Операция";
            }
        }

        /// <summary>Kind label for the shop rows.</summary>
        public static string Kind(string kind)
        {
            switch (kind)
            {
                case "skin": return "Скин";
                case "cape": return "Плащ";
                case "emote": return "Эмоция";
                case "bundle": return "Набор";
                default: return "Предмет";
            }
        }

        /// <summary>"Игрок #ab12" for keys that carry no profile name.</summary>
        public static string ShortKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "Игрок";
            return "Игрок #" + (key.Length > 4 ? key.Substring(0, 4) : key);
        }
    }

    /// <summary>Skin colours come from the database as three hex strings.</summary>
    public static class SkinPalette
    {
        public static readonly string[] Default = { "#c8b48a", "#4b6b5c", "#231a14" };
        public const int Colors = 3;

        /// <summary>
        /// Returns exactly three valid #rrggbb strings, falling back per entry.
        /// Anything a player cannot send is impossible, but old rows may still be
        /// malformed — the renderer must never break because of a skin.
        /// </summary>
        public static string[] Sanitize(string[] palette)
        {
            var result = new string[Colors];
            for (int i = 0; i < Colors; i++)
                result[i] = Valid(palette != null && i < palette.Length ? palette[i] : null) ? Normalize(palette[i]) : Default[i];
            return result;
        }

        public static bool Valid(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char c = hex[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }

        static string Normalize(string hex)
        {
            if (!Valid(hex)) return null;
            var text = new StringBuilder("#");
            for (int i = 1; i < 7; i++) text.Append(char.ToLowerInvariant(hex[i]));
            return text.ToString();
        }

        /// <summary>0..1 channel from a #rrggbb string, used to tint the avatar.</summary>
        public static float Channel(string hex, int channel)
        {
            string clean = Valid(hex) ? hex : Default[0];
            if (channel < 0 || channel > 2) return 0f;
            int start = 1 + channel * 2;
            int value = Hex(clean[start]) * 16 + Hex(clean[start + 1]);
            return value / 255f;
        }

        static int Hex(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }
    }
}
