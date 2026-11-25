using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LestingCraft.Gameplay;
using LestingCraft.Networking;
using LestingCraft.Presentation;
using LestingCraft.Runtime;
using LestingCraft.World;

namespace LestingCraft.Tests
{
    public sealed class InfernoLanAndMarketTests
    {
        [Test]
        public void InfernoDimensionWorldgenPortalAndBossAreValid()
        {
            Assert.AreEqual(16, (int)Dimension.Inferno);
            Assert.AreEqual(16, (int)PlanetBody.Inferno);

            int seed = 2506;
            // Spawn sanctuary around (0, 0) must be safe solid platform above LavaLevel
            int spawnTop = InfernoTerrain.FloorTop(0, 0, seed);
            Assert.Greater(spawnTop, InfernoTerrain.LavaLevel + 4, "Spawn sanctuary must stand safely above lava");
            Assert.AreNotEqual(InfernoTerrain.LavaId, InfernoTerrain.Block(0, spawnTop, 0, seed, spawnTop));
            Assert.AreEqual(0, InfernoTerrain.Block(0, spawnTop + 2, 0, seed, spawnTop), "Spawn headroom must be air");

            // Return Hellgate at (0, -8) must register as exit threshold and contain portal crystal
            Assert.IsTrue(InfernoTerrain.IsExitThreshold(0, -8, seed), "Spawn return Hellgate threshold must be active");

            // Cavernous bedrock ceiling and lava seas must exist across chunks
            bool sawLavaSea = false, sawCeiling = false, sawFortress = false;
            for (int z = -96; z <= 96; z += 4)
            for (int x = -96; x <= 96; x += 4)
            {
                var col = PlanetTerrain.Column(x, z, seed, PlanetBody.Inferno);
                ushort atLava = PlanetTerrain.Block(x, InfernoTerrain.LavaLevel, z, seed, col, PlanetBody.Inferno);
                if (atLava == InfernoTerrain.LavaId) sawLavaSea = true;
                ushort atRoof = PlanetTerrain.Block(x, InfernoTerrain.CeilingTop, z, seed, col, PlanetBody.Inferno);
                if (atRoof == InfernoTerrain.BedrockId) sawCeiling = true;
            }
            // Guaranteed Infernal Citadel near (68, 52)
            Assert.IsTrue(InfernoTerrain.TryGetCitadel(0, 0, seed, out int cx, out int cz));
            Assert.AreEqual(68, cx);
            Assert.AreEqual(52, cz);
            var citCol = PlanetTerrain.Column(cx - 17, cz - 17, seed, PlanetBody.Inferno);
            for (int y = 34; y <= 60; y++)
            {
                ushort b = PlanetTerrain.Block(cx - 17, y, cz - 17, seed, citCol, PlanetBody.Inferno);
                if (b == InfernoTerrain.ObsidianId || b == InfernoTerrain.MagmaBasaltId) sawFortress = true;
            }

            Assert.IsTrue(sawLavaSea, "Inferno must feature glowing lava seas");
            Assert.IsTrue(sawCeiling, "Inferno must have an enclosed cavernous ceiling");
            Assert.IsTrue(sawFortress, "Inferno must generate Obsidian Citadels");

            // Boss & Overworld Hellgate Shrine
            var boss = BossCatalog.For(Dimension.Inferno);
            Assert.AreEqual("Владыка Преисподней", boss.Title);
            Assert.GreaterOrEqual(boss.MaxHealth, 450);

            Assert.IsTrue(RealmWondersPlan.TryGetWonderSite(0, 1, seed, out int hx, out int hy, out int hz, out int hkind), "Guaranteed Obsidian Hellgate Shrine must exist in cell (0, 1)");
            Assert.AreEqual(4, hkind);
            Assert.IsTrue(RealmWondersPlan.IsInHellgateDoorway(hx, hy + 2, hz, seed), "Stepping into Hellgate doorway must trigger travel");
        }

        [Test]
        public void CustomHellgateFrameDetectionRecognizesObsidianPortal()
        {
            var blocks = new Dictionary<Vector3Int, ushort>();
            // Build a 4x5 Obsidian frame along X at z = 10, base y = 50..54, x = 0..3
            for (int x = 0; x <= 3; x++)
            {
                blocks[new Vector3Int(x, 50, 10)] = 68;
                blocks[new Vector3Int(x, 54, 10)] = 68;
            }
            for (int y = 50; y <= 54; y++)
            {
                blocks[new Vector3Int(0, y, 10)] = 68;
                blocks[new Vector3Int(3, y, 10)] = 68;
            }
            ushort Getter(int x, int y, int z) => blocks.TryGetValue(new Vector3Int(x, y, z), out var id) ? id : (ushort)0;

            Assert.IsTrue(RealmFeaturesAndMarket.IsCustomPortalFrameReady(Getter, new Vector3Int(1, 50, 10)));
            Assert.IsFalse(RealmFeaturesAndMarket.IsCustomPortalFrameReady(Getter, new Vector3Int(20, 50, 10)));
        }

        [Test]
        public void LocalLanServerServesHttpDirectoryAndWebSocketRoomWithPreloadedEdits()
        {
            var preloaded = new[]
            {
                new SavedBlock { realm = 0, x = 12, y = 66, z = -4, block = 40 },
                new SavedBlock { realm = 0, x = 13, y = 66, z = -4, block = 56 }
            };
            var server = LocalLanServer.StartOrReplace("Тестовый LAN Мир", "Tester", 2506, 0, 8, preloaded, 18791);
            try
            {
                Assert.IsTrue(server.IsRunning);
                Assert.AreEqual(6, server.ShortCode.Length);

                using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                {
                    string listJson = wc.DownloadString("http://127.0.0.1:" + server.Port + "/api/servers");
                    StringAssert.Contains(server.RoomCode, listJson);
                    StringAssert.Contains("Тестовый LAN Мир", listJson);
                }

                using (var ws = new ClientWebSocket())
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    ws.ConnectAsync(new Uri("ws://127.0.0.1:" + server.Port + "/api/rooms/" + server.RoomCode + "/ws"), cts.Token).GetAwaiter().GetResult();

                    var buf = new byte[8192];
                    var res = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token).GetAwaiter().GetResult();
                    string welcome = Encoding.UTF8.GetString(buf, 0, res.Count);
                    StringAssert.Contains("\"type\":\"welcome\"", welcome);
                    StringAssert.Contains("\"x\":12,\"y\":66,\"z\":-4,\"block\":40", welcome);

                    // Consume join announcement chat
                    res = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token).GetAwaiter().GetResult();
                    StringAssert.Contains("\"type\":\"chat\"", Encoding.UTF8.GetString(buf, 0, res.Count));

                    // Verify live block edit broadcast
                    byte[] editBytes = Encoding.UTF8.GetBytes("{\"type\":\"edit\",\"x\":10,\"y\":70,\"z\":10,\"block\":56}");
                    ws.SendAsync(new ArraySegment<byte>(editBytes), WebSocketMessageType.Text, true, cts.Token).GetAwaiter().GetResult();
                    res = ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token).GetAwaiter().GetResult();
                    string editBroadcast = Encoding.UTF8.GetString(buf, 0, res.Count);
                    StringAssert.Contains("\"type\":\"edit\"", editBroadcast);
                    StringAssert.Contains("\"block\":56", editBroadcast);
                }
            }
            finally
            {
                LocalLanServer.StopActive();
                Assert.IsNull(LocalLanServer.Active);
            }
        }

        [Test]
        public void MarketplaceTradesBlueprintsContractsWaypointsAndMinimapWorkOffline()
        {
            int oldCoins = RealmFeaturesAndMarket.GuildCoins;
            try
            {
                RealmFeaturesAndMarket.GuildCoins = 500;
                var inv = new Inventory();
                inv.Add(4, 32); // 32 wood

                // 1. Sell resource offer (12 wood -> +40 coins)
                var sellWood = RealmFeaturesAndMarket.SellOffers[0];
                Assert.IsTrue(RealmFeaturesAndMarket.TryExecuteTrade(sellWood, inv, out _));
                Assert.AreEqual(500 + sellWood.CoinValue, RealmFeaturesAndMarket.GuildCoins);
                Assert.AreEqual(32 - sellWood.Count, inv.Count(4));

                // 2. Buy supply offer
                int beforeBuy = RealmFeaturesAndMarket.GuildCoins;
                var buyKit = RealmFeaturesAndMarket.BuySupplies[0];
                Assert.IsTrue(RealmFeaturesAndMarket.TryExecuteTrade(buyKit, inv, out _));
                Assert.AreEqual(beforeBuy - buyKit.CoinValue, RealmFeaturesAndMarket.GuildCoins);
                Assert.GreaterOrEqual(inv.Count(buyKit.ItemId), buyKit.Count);

                // 3. All 8 Blueprints place valid blocks & Hellgate Arch includes portal crystal (67)
                foreach (var bp in RealmFeaturesAndMarket.Blueprints)
                {
                    var placed = new Dictionary<Vector3Int, ushort>();
                    int count = RealmFeaturesAndMarket.ConstructBlueprint(bp.Code, 100, 64, 100, (x, y, z, id) =>
                    {
                        if (id != 0) placed[new Vector3Int(x, y, z)] = id;
                    });
                    Assert.Greater(count, 20, "Blueprint " + bp.Code + " must build a substantial 3D structure");
                    if (bp.Code == "hellgate_arch")
                        Assert.IsTrue(placed.ContainsValue(67), "Hellgate blueprint must include active portal crystal");
                }

                // 4. Waypoints & Minimap rendering
                int testSeed = 918273;
                RealmFeaturesAndMarket.ClearWaypoints(testSeed);
                RealmFeaturesAndMarket.SetOrUpdateWaypoint(testSeed, "Дом", new Vector3(14, 70, -22), Dimension.Lestingia, "home");
                Assert.IsTrue(RealmFeaturesAndMarket.TryGetWaypoint(testSeed, "home", out var homeWp));
                Assert.AreEqual(14, homeWp.x);

                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                try
                {
                    RealmFeaturesAndMarket.RenderMinimap(tex, 2506, Dimension.Lestingia, new Vector3(0, 70, 0), 45f, RealmFeaturesAndMarket.LoadWaypoints(testSeed));
                    Assert.Greater(tex.GetPixel(32, 32).r, 0.9f, "Minimap center must highlight player position");
                    RealmFeaturesAndMarket.RenderMinimap(tex, 2506, Dimension.Inferno, new Vector3(0, 42, 0), 0f, null);
                    Assert.Greater(tex.GetPixel(32, 32).r, 0.9f);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                    RealmFeaturesAndMarket.ClearWaypoints(testSeed);
                }
            }
            finally
            {
                RealmFeaturesAndMarket.GuildCoins = oldCoins;
            }
        }

        [Test]
        public void NewChatCommandsAndUiHubsNavigateCleanly()
        {
            var inv = new Inventory();
            Assert.AreEqual("sethome", ChatCommands.Execute("/sethome", false, true, inv, 2506, Vector3.zero, 1).Action);
            Assert.AreEqual("home", ChatCommands.Execute("/home", false, true, inv, 2506, Vector3.zero, 1).Action);
            Assert.AreEqual("lan", ChatCommands.Execute("/lan", false, true, inv, 2506, Vector3.zero, 1).Action);
            Assert.AreEqual("market", ChatCommands.Execute("/market", false, true, inv, 2506, Vector3.zero, 1).Action);
            Assert.AreEqual("inferno", ChatCommands.Execute("/hell", true, true, inv, 2506, Vector3.zero, 1).Action);
            var tp = ChatCommands.Execute("/tp 12 80 -35", true, true, inv, 2506, Vector3.zero, 1);
            Assert.AreEqual("tp:12:80:-35", tp.Action);

            var prevEventSystem = EventSystem.current;
            var owner = new GameObject("UI Hub Test Owner");
            owner.SetActive(false);
            var uiGo = new GameObject("UI Hub Test Canvas");
            try
            {
                var game = owner.AddComponent<PrototypeGame>();
                VisualSettings.Load();
                var ui = uiGo.AddComponent<GameUI>();
                ui.Initialize(game);

                // Quick Marketplace button from Home works offline across all 4 tabs
                var homeMarket = FindBtn(ui, "Home.Market");
                Assert.IsNotNull(homeMarket, "Home.Market button must exist in top-right header");
                homeMarket.onClick.Invoke();
                Assert.AreEqual("Market", ui.ScreenName);
                for (int t = 0; t < 4; t++)
                {
                    var tabBtn = FindBtn(ui, "Market.Tab." + t);
                    Assert.IsNotNull(tabBtn, "Market tab " + t + " must exist");
                    tabBtn.onClick.Invoke();
                    Assert.AreEqual("Market", ui.ScreenName);
                }

                // Multiplayer Rooms + Server Creator Studio
                ui.ShowRooms();
                Assert.IsNotNull(FindBtn(ui, "Rooms.QuickJoin"));
                Assert.IsNotNull(FindBtn(ui, "Rooms.LanHost"));
                var hostBtn = FindBtn(ui, "Rooms.Host");
                Assert.IsNotNull(hostBtn);
                hostBtn.onClick.Invoke();
                Assert.AreEqual("AddServer", ui.ScreenName);
                Assert.IsNotNull(FindBtn(ui, "AddServer.Create"));

                // Pause menu includes LAN & Marketplace shortcuts
                ui.ShowPause();
                Assert.IsNotNull(FindBtn(ui, "Pause.Market"));
                Assert.IsNotNull(FindBtn(ui, "Pause.HostLan"));
                Assert.IsNotNull(FindBtn(ui, "Pause.HostServer"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(uiGo);
                UnityEngine.Object.DestroyImmediate(owner);
                if (!prevEventSystem && EventSystem.current)
                    UnityEngine.Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        static Button FindBtn(GameUI ui, string name)
        {
            foreach (var b in ui.GetComponentsInChildren<Button>(true))
                if (b.name == name) return b;
            return null;
        }
    }
}
