// Compile-time and behaviour check for the shop/wallet wire models.
// mcs -langversion:7.2 Assets/Scripts/Gameplay/CloudModels.cs Tools/check_cloud_core.cs
using System;
using LestingCraft.Gameplay;

static class CheckCloudCore
{
    static int failures;
    static void Check(bool condition, string message)
    {
        if (condition) return;
        failures++;
        Console.WriteLine("FAIL: " + message);
    }

    static int Main()
    {
        Money();
        Palette();
        Skins();
        Console.WriteLine(failures == 0 ? "PASS cloud models" : failures + " failures");
        return failures == 0 ? 0 : 1;
    }

    static void Money()
    {
        Check(CloudFormat.Coins(0) == "0", "zero coins");
        Check(CloudFormat.Coins(7) == "7", "single digit");
        Check(CloudFormat.Coins(999) == "999", "three digits stay ungrouped");
        Check(CloudFormat.Coins(1000) == "1 000", "thousands are grouped");
        Check(CloudFormat.Coins(1234567) == "1 234 567", "millions are grouped");
        Check(CloudFormat.Coins(-450) == "−450", "negative uses a minus sign");

        Check(CloudFormat.Price(199, "USD") == "$1.99", "usd cents: " + CloudFormat.Price(199, "USD"));
        Check(CloudFormat.Price(99, "USD") == "$0.99", "single cent keeps the leading zero");
        Check(CloudFormat.Price(1000, "usd") == "$10.00", "whole currency units");
        Check(CloudFormat.Price(1999, "EUR") == "€19.99", "euro");
        Check(CloudFormat.Price(1999, "RUB") == "₽19.99", "rouble");
        Check(CloudFormat.Price(1999, "GBP") == "GBP 19.99", "unknown currency falls back to its code");
        Check(CloudFormat.Price(1999, null) == "$19.99", "missing currency means USD");

        Check(CloudFormat.Reason("topup") == "Покупка монет", "topup label");
        Check(CloudFormat.Reason("market_sale") == "Продажа лота", "sale label");
        Check(CloudFormat.Reason("something_new") == "Операция", "unknown reasons stay readable");

        Check(CloudFormat.Kind("skin") == "Скин" && CloudFormat.Kind("cape") == "Плащ", "item kinds");
        Check(CloudFormat.ShortKey("").Length > 0, "empty key still has a label");
        Check(CloudFormat.ShortKey("abcdef0123456789").Contains("abcd"), "short key keeps a prefix");
    }

    static void Palette()
    {
        Check(SkinPalette.Valid("#ff9900"), "valid hex accepted");
        Check(SkinPalette.Valid("#FF9900"), "uppercase hex accepted");
        Check(!SkinPalette.Valid("#ff990"), "five digits are not a colour");
        Check(!SkinPalette.Valid("ff9900"), "missing hash is not a colour");
        Check(!SkinPalette.Valid("#gg9900"), "non hex digits are rejected");
        Check(!SkinPalette.Valid(null), "null is not a colour");

        var fixedPalette = SkinPalette.Sanitize(new[] { "#FF0000", "oops", null });
        Check(fixedPalette.Length == 3, "sanitize always returns three colours");
        Check(fixedPalette[0] == "#ff0000", "valid entries are normalised to lowercase");
        Check(fixedPalette[1] == SkinPalette.Default[1] && fixedPalette[2] == SkinPalette.Default[2], "broken entries fall back");

        var fromEmpty = SkinPalette.Sanitize(null);
        for (int i = 0; i < 3; i++) Check(fromEmpty[i] == SkinPalette.Default[i], "null palette falls back entirely");

        Check(Math.Abs(SkinPalette.Channel("#ffffff", 0) - 1f) < 0.001f, "white is 1");
        Check(Math.Abs(SkinPalette.Channel("#000000", 2)) < 0.001f, "black is 0");
        Check(Math.Abs(SkinPalette.Channel("#00ff80", 1) - 1f) < 0.001f, "green channel");
        Check(Math.Abs(SkinPalette.Channel("#00ff80", 2) - 0.5019f) < 0.01f, "half blue channel");
        Check(SkinPalette.Channel("#ff0000", 5) == 0f, "out of range channel is safe");
        Check(Math.Abs(SkinPalette.Channel("nonsense", 0) - 0.784f) < 0.01f, "invalid colour falls back to the default");
    }

    static void Skins()
    {
        var wallet = new CloudState { balance = 164, profile = new CloudProfile { avatar_skin = "skin_ember" } };
        Check(wallet.balance == 164, "wallet balance field");
        Check(wallet.profile.avatar_skin == "skin_ember", "equipped skin field");

        var ember = new CloudSkin
        {
            code = "skin_ember", kind = "skin", title = "Уголёк", price = 450,
            payload = new CloudPayload { palette = new[] { "#efba69", "#8c3b1e", "#2a1710" } },
        };
        wallet.owned = new[] { ember };
        wallet.store = new[] { ember };
        Check(wallet.owned.Length == 1 && wallet.store[0].payload.palette.Length == 3, "catalogue payload");
        Check(CloudFormat.Coins(wallet.store[0].price) == "450", "price is formatted for the row");

        var pack = new CloudPack { coins = 1200, bonus = 100, price_minor = 499, currency = "USD", code = "satchel" };
        Check(pack.CoinsText == "1 300", "pack shows coins plus bonus");
        Check(pack.PriceText == "$4.99", "pack shows its price");

        var listing = new CloudListing { id = "0b6d5c1e-1a1a-4a4a-9a9a-123456789abc", price = 120, mine = true };
        Check(listing.mine && listing.price == 120, "listing fields");

        var ledger = new CloudLedgerRow { delta = -120, reason = "market_purchase", balance_after = 1180 };
        Check(CloudFormat.Reason(ledger.reason) == "Покупка лота" && ledger.balance_after == 1180, "ledger row");

        var friends = new CloudFriendsReply
        {
            friends = new[] { new CloudFriend { key = "abc", name = "Аня", online = true } },
            incoming = new[] { new CloudFriendRequest { id = 10, name = "Костя", message = "играем?" } },
        };
        Check(friends.friends[0].online && friends.incoming[0].id == 10, "friend reply fields");
    }
}
