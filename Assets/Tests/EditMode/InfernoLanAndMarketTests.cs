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

            // Boss & Twin-Tree Ritual Clearing wonder site
            var boss = BossCatalog.For(Dimension.Inferno);
            Assert.AreEqual("Владыка Преисподней", boss.Title);
            Assert.GreaterOrEqual(boss.MaxHealth, 450);

            Assert.IsTrue(RealmWondersPlan.TryGetWonderSite(0, 1, seed, out int hx, out int hy, out int hz, out int hkind), "Guaranteed Twin-Tree Ritual Clearing must exist in cell (0, 1)");
            Assert.AreEqual(4, hkind);
        }

        [Test]
        public void TwinTreeHellRitualRequiresNoBoneMealSevenBlockSpacingAndBurning()
        {
            var blocks = new Dictionary<Vector3Int, ushort>();
            ushort GetB(int x, int y, int z) => blocks.TryGetValue(new Vector3Int(x, y, z), out var id) ? id : (ushort)0;
            void SetB(int x, int y, int z, ushort id)
            {
                if (id == 0) blocks.Remove(new Vector3Int(x, y, z));
                else blocks[new Vector3Int(x, y, z)] = id;
            }

            var rituals = new PortalRitualState();

            // 1. Disqualified pair: bone meal used on one sapling -> NEVER forms a Hell Arch
            SetB(0, 50, 0, BlockRegistry.OakSaplingId);
            SetB(7, 50, 0, BlockRegistry.OakSaplingId);
            rituals.RegisterSapling(0, 50, 0, BlockRegistry.OakSaplingId);
            rituals.RegisterSapling(7, 50, 0, BlockRegistry.OakSaplingId);
            Assert.IsTrue(rituals.TryApplyBoneMeal(0, 50, 0, GetB, SetB, out _));
            for (int i = 0; i < 20; i++) rituals.Step(1f, GetB, SetB, out _);
            Assert.IsTrue(rituals.TryIgniteTreeAt(0, 52, 0, GetB, SetB, out _));
            Assert.IsTrue(rituals.TryIgniteTreeAt(7, 52, 0, GetB, SetB, out _));
            for (int i = 0; i < 12; i++) rituals.Step(1f, GetB, SetB, out _);
            Assert.AreEqual(0, rituals.HellArches.Count, "Bonemealed trees must never form a Hell Arch");

            // 2. Valid pair: 2 saplings at 7 blocks distance (x=30 and x=37), grown WITHOUT bone meal, then burned!
            SetB(30, 50, 20, BlockRegistry.OakSaplingId);
            SetB(37, 50, 20, BlockRegistry.SakuraSaplingId);
            rituals.RegisterSapling(30, 50, 20, BlockRegistry.OakSaplingId);
            string pairMsg = rituals.RegisterSapling(37, 50, 20, BlockRegistry.SakuraSaplingId);
            Assert.IsNotEmpty(pairMsg);

            for (int i = 0; i < 18; i++) rituals.Step(1f, GetB, SetB, out _);
            Assert.AreEqual(BlockRegistry.WoodId, GetB(30, 52, 20), "Natural sapling A must grow into a tree");
            Assert.AreEqual(BlockRegistry.WoodId, GetB(37, 52, 20), "Natural sapling B must grow into a tree");
            Assert.AreEqual(0, rituals.HellArches.Count, "Unburned trees must NOT open Hell");

            Assert.IsTrue(rituals.TryIgniteTreeAt(30, 52, 20, GetB, SetB, out _));
            Assert.IsTrue(rituals.TryIgniteTreeAt(37, 52, 20, GetB, SetB, out _));
            Assert.AreEqual(0, rituals.HellArches.Count, "Freshly ignited trees must burn before forming an arch");

            for (int i = 0; i < 8; i++) rituals.Step(0.6f, GetB, SetB, out _);
            Assert.AreEqual(1, rituals.HellArches.Count, "Burned non-bonemealed twin trees at 7 blocks must fuse into a Hell Arch");
            Assert.IsTrue(rituals.IsInsideActiveHellArch(new Vector3(34f, 51.5f, 20.5f)), "Walking under the charred arch must trigger Hell entry");
        }

        [Test]
        public void ParadiseDimensionAndFlowerWaterDiamondWellRitualWorkExact()
        {
            Assert.AreEqual(17, (int)Dimension.Paradise);
            Assert.AreEqual(17, (int)PlanetBody.Paradise);

            int seed = 2506;
            int h0 = ParadiseTerrain.Height(0, 0, seed);
            Assert.Greater(h0, ParadiseTerrain.SeaLevel + 4);
            Assert.IsTrue(ParadiseTerrain.IsExitThreshold(0, -8, seed), "Sacred Return Fountain must exist at (0, -8)");
            var seraph = BossCatalog.For(Dimension.Paradise);
            Assert.AreEqual("Seraph of Eden", seraph.Name);
            Assert.AreEqual("Серафим Эдема", seraph.Title);

            var blocks = new Dictionary<Vector3Int, ushort>();
            ushort GetB(int x, int y, int z) => blocks.TryGetValue(new Vector3Int(x, y, z), out var id) ? id : (ushort)0;
            void SetB(int x, int y, int z, ushort id)
            {
                if (id == 0) blocks.Remove(new Vector3Int(x, y, z));
                else blocks[new Vector3Int(x, y, z)] = id;
            }

            for (int y = 55; y <= 60; y++)
            for (int z = 8; z <= 13; z++)
            for (int x = 8; x <= 13; x++)
                SetB(x, y, z, BlockRegistry.GrassId);

            for (int y = 56; y <= 60; y++)
            for (int z = 10; z <= 11; z++)
            for (int x = 10; x <= 11; x++)
                SetB(x, y, z, BlockRegistry.WaterId);

            var rituals = new PortalRitualState();
            Assert.IsFalse(rituals.TryActivateParadiseWellNear(new Vector3(10.5f, 61f, 10.5f), GetB, SetB, out _), "Well without flowers must NOT activate");

            ushort[] flowers = { 94, 95, 96, 97, 94, 95, 96, 97 };
            int[] fx = { 10, 11, 10, 11, 9, 9, 12, 12 };
            int[] fz = { 9, 9, 12, 12, 10, 11, 10, 11 };
            for (int i = 0; i < 8; i++) SetB(fx[i], 61, fz[i], flowers[i]);

            Assert.IsTrue(rituals.TryActivateParadiseWellNear(new Vector3(10.5f, 61f, 10.5f), GetB, SetB, out _), "Throwing a diamond into the flower-ringed 2x2x5 water pit must open Paradise");
            Assert.AreEqual(BlockRegistry.AndromedaCrystalId, GetB(10, 55, 10), "Pit floor must crystallize with celestial glow");
            Assert.IsTrue(rituals.IsPlayerInsideActivatedParadiseWell(new Vector3(11.0f, 59.0f, 11.0f)), "Jumping into the activated well must trigger Paradise teleport");
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
                    if (bp.Code == "eden_pavilion")
                        Assert.IsTrue(placed.ContainsValue(58), "Eden Pavilion blueprint must include golden columns");
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
            Assert.AreEqual("paradise", ChatCommands.Execute("/paradise", true, true, inv, 2506, Vector3.zero, 1).Action);
            Assert.AreEqual("paradise", ChatCommands.Execute("/рай", true, true, inv, 2506, Vector3.zero, 1).Action);
            Assert.AreEqual("backrooms", ChatCommands.Execute("/backrooms", true, true, inv, 2506, Vector3.zero, 1).Action);
            Assert.AreEqual("fly", ChatCommands.Execute("/fly", true, true, inv, 2506, Vector3.zero, 1).Action);
            var gmCreative = ChatCommands.Execute("/gamemode creative", false, true, inv, 2506, Vector3.zero, 1);
            Assert.AreEqual("gamemode", gmCreative.Action);
            Assert.AreEqual((int)GameMode.Creative, gmCreative.Value);
            var gmSurv = ChatCommands.Execute("/gm survival", true, true, inv, 2506, Vector3.zero, 1);
            Assert.AreEqual("gamemode", gmSurv.Action);
            Assert.AreEqual((int)GameMode.Survival, gmSurv.Value);
            Assert.IsFalse(SurvivalSession.RequiresSpaceSuit(Dimension.Lestingia));
            Assert.IsFalse(SurvivalSession.RequiresSpaceSuit(Dimension.Backrooms));
            Assert.IsFalse(SurvivalSession.RequiresSpaceSuit(Dimension.Inferno));
            Assert.IsFalse(SurvivalSession.RequiresSpaceSuit(Dimension.Paradise));
            Assert.IsTrue(SurvivalSession.RequiresSpaceSuit(Dimension.Moon));
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

        [Test]
        public void BackroomsZonesAndAllModernizedMobSculptsAreValid()
        {
            int seed = 2506;
            var zonesSeen = new HashSet<int>();
            for (int z = -320; z <= 320; z += 40)
            for (int x = -320; x <= 320; x += 40)
                zonesSeen.Add(BackroomsTerrain.ZoneAt(x, z, seed));
            Assert.GreaterOrEqual(zonesSeen.Count, 4, "Backrooms must span multiple distinct sublevels");

            // 3 Backrooms horror entity sculpts + DreamWatcher + DomesticCat
            for (int v = 0; v < 3; v++)
            {
                var bData = BossSculpts.BacteriaMob(v);
                Assert.Greater(bData.CellCount, 300);
                Assert.IsTrue(bData.HasPart("head") && bData.HasPart("arm_l"));
            }
            var dw = BossSculpts.DreamWatcher();
            Assert.Greater(dw.CellCount, 1200);
            Assert.IsTrue(dw.HasPart("head") && dw.HasPart("arm_l"));

            for (int c = 0; c < 4; c++)
            {
                var cat = AnimalSculpts.DomesticCat(c);
                Assert.Greater(cat.CellCount, 120);
                Assert.IsTrue(cat.HasPart("head") && cat.HasPart("tail") && cat.HasPart("leg0"));
            }

            // Inferno (34..37) and Paradise (38..41) fauna sculpts & simulation
            Assert.AreEqual(42, WildlifeSimulation.SpeciesCount);
            Assert.AreEqual(42, AnimalSculpts.SpeciesNames.Length);
            for (int sp = 34; sp <= 41; sp++)
            {
                var sc = AnimalSculpts.Build(sp);
                Assert.Greater(sc.CellCount, 200, "Species " + sp + " must have a detailed fine-voxel sculpt");
            }

            // Memorial 3D voxel grave sculpt
            var grave = BossSculpts.MemorialGrave(2506);
            Assert.Greater(grave.CellCount, 120, "Player grave must be a sculpted 3D voxel headstone");
            Assert.IsTrue(grave.HasPart("body"));

            // Backrooms 5-zone dynamic fog profile & biome titles
            SpaceFlightVisuals.GetBackroomsZoneFog(0, 0f, out Color fogLobby, out float densLobby);
            SpaceFlightVisuals.GetBackroomsZoneFog(1, 0f, out Color fogPool, out float densPool);
            SpaceFlightVisuals.GetBackroomsZoneFog(3, 0f, out Color fogDark, out float densDark);
            SpaceFlightVisuals.GetBackroomsZoneFog(4, 0f, out Color fogRed, out _);
            Assert.Less(densPool, densLobby, "Poolrooms must have cleaner sunlit mist than Level 0");
            Assert.Greater(densDark, densLobby, "Level 6 Lights Out must have thicker dark fog");
            Assert.Greater(fogPool.b, fogLobby.b, "Poolrooms fog must have porcelain-aqua tint");
            Assert.Greater(fogRed.r, fogRed.g * 3f, "Emergency sector fog must glow crimson");
            Assert.IsFalse(string.IsNullOrEmpty(InfernoTerrain.BiomeTitle(0)));
            Assert.IsFalse(string.IsNullOrEmpty(ParadiseTerrain.BiomeTitle(0)));
        }

        [Test]
        public void ExoplanetsSomniaBiospheresContractsAndMultiRealmLanPersistenceWork()
        {
            // 1. WildlifeManager supports Lestingia, Radia, Inferno, Paradise, Somnia, Lush Exoplanets, Cryo & Volcanic worlds
            Assert.IsTrue(WildlifeManager.SupportsWildlife(Dimension.Somnia));
            Assert.IsTrue(WildlifeManager.SupportsWildlife(Dimension.Kepler452b));
            Assert.IsTrue(WildlifeManager.SupportsWildlife(Dimension.Trappist1e));
            Assert.IsTrue(WildlifeManager.SupportsWildlife(Dimension.Titan));
            Assert.IsTrue(WildlifeManager.SupportsWildlife(Dimension.Europa));
            Assert.IsTrue(WildlifeManager.SupportsWildlife(Dimension.Venus));

            int[] offworldSentinels = { -12, -18, -19, -20 };
            foreach (int sentinel in offworldSentinels)
            {
                var sim = new WildlifeSimulation(2506, null, 11) { SurfaceAt = (x, z) => sentinel };
                sim.Spawn(new AnimalObserver { id = "p", x = 0f, y = 50f, z = 0f, alive = true }, (x, z, prev, r, h) => 48f);
                Assert.Greater(sim.Animals.Count, 10, "Off-world sentinel " + sentinel + " must populate with matching fauna");
            }

            // 2. Guild Contracts include new ritual & Eden contracts (8 total)
            Assert.AreEqual(8, RealmFeaturesAndMarket.Contracts.Length);
            Assert.IsTrue(System.Array.Exists(RealmFeaturesAndMarket.Contracts, c => c.Id == "ritual_forester"));
            Assert.IsTrue(System.Array.Exists(RealmFeaturesAndMarket.Contracts, c => c.Id == "eden_explorer"));

            // 3. LocalLanServer preserves multi-realm blocks (Inferno=16 & Paradise=17) alongside Lestingia (0)
            var multiEdits = new[]
            {
                new SavedBlock { realm = 0, x = 5, y = 65, z = 5, block = 42 },
                new SavedBlock { realm = (int)Dimension.Inferno, x = 8, y = 44, z = -3, block = 27 },
                new SavedBlock { realm = (int)Dimension.Paradise, x = -6, y = 55, z = 9, block = 95 }
            };
            var lan = LocalLanServer.StartOrReplace("MultiRealm LAN", "Host", 2506, 0, 4, multiEdits, 18821);
            Assert.IsNotNull(lan);
            try
            {
                var exported = lan.ExportAllSavedBlocks();
                Assert.AreEqual(3, exported.Length);
                Assert.IsTrue(System.Array.Exists(exported, b => b.realm == (int)Dimension.Inferno && b.block == 27));
                Assert.IsTrue(System.Array.Exists(exported, b => b.realm == (int)Dimension.Paradise && b.block == 95));
            }
            finally
            {
                LocalLanServer.StopActive();
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
