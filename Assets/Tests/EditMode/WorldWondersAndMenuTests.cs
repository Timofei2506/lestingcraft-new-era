using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.Runtime;
using LestingCraft.World;

namespace LestingCraft.Tests
{
    public sealed class WorldWondersAndMenuTests
    {
        [Test]
        public void FloatingSkyIslandsAndCascadingWaterfallsGenerateAndMatchBurstChunkJob()
        {
            const int seed = 2506;
            // Guaranteed Horizon Sky Island #1 in cell (-1, -1): center (-148, 138, -72)
            Assert.IsTrue(RealmWondersPlan.TryGetSkyIslandCenter(-1, -1, seed, out int icx, out int deckY, out int icz, out int rad));
            Assert.AreEqual(-148, icx);
            Assert.AreEqual(138, deckY);
            Assert.AreEqual(-72, icz);
            Assert.GreaterOrEqual(rad, 22);

            // Sky-shrine centerpiece at (icx, deckY+1..deckY+10, icz)
            Assert.AreEqual(42, TerrainGenerator.SampleContent(icx, deckY + 2, icz, seed, Dimension.Lestingia, 11), "Central sky-shrine marble column");
            Assert.AreEqual(11, TerrainGenerator.SampleContent(icx, deckY + 7, icz, seed, Dimension.Lestingia, 11), "Central sky-shrine crystal spire");
            Assert.AreEqual(56, TerrainGenerator.SampleContent(icx, deckY + 10, icz, seed, Dimension.Lestingia, 11), "Central sky-shrine crown lantern");

            // Legacy contentVersion <= 10 must remain untouched (air at sky island height)
            Assert.AreEqual(0, TerrainGenerator.SampleContent(icx, deckY, icz, seed, Dimension.Lestingia, 10));

            // Find cascading sky waterfall column around the island rim and verify water falls through mid-air (y = 96)
            bool foundSkyWaterfall = false;
            int fallX = icx, fallZ = icz;
            for (int dz = -rad - 2; dz <= rad + 2 && !foundSkyWaterfall; dz++)
                for (int dx = -rad - 2; dx <= rad + 2 && !foundSkyWaterfall; dx++)
                {
                    if (TerrainGenerator.SampleContent(icx + dx, 96, icz + dz, seed, Dimension.Lestingia, 11) == BlockRegistry.WaterId)
                    {
                        foundSkyWaterfall = true;
                        fallX = icx + dx;
                        fallZ = icz + dz;
                    }
                }
            Assert.IsTrue(foundSkyWaterfall, "Floating sky island must pour a real cascading waterfall down into the valley");

            // Verify exact bit-for-bit parity between SampleContent and [BurstCompile] GenerateChunkJob across sky island & waterfallheights (y = 1..200)
            using (var blocks = new NativeArray<ushort>(ChunkData.BlockCount, Allocator.TempJob))
            {
                int[] probeX = { icx, fallX, -188, -84 };
                int[] probeZ = { icz, fallZ, 52, -232 };
                for (int p = 0; p < probeX.Length; p++)
                {
                    int wx = probeX[p], wz = probeZ[p];
                    int ox = Mathf.FloorToInt(wx / 16f) * 16, oz = Mathf.FloorToInt(wz / 16f) * 16;
                    var job = new GenerateChunkJob
                    {
                        OriginX = ox,
                        OriginZ = oz,
                        Seed = seed,
                        ContentVersion = 11,
                        Dimension = Dimension.Lestingia,
                        Blocks = blocks
                    };
                    int lx = wx - ox, lz = wz - oz;
                    job.Execute(lx + 16 * lz);
                    for (int y = 1; y <= 200; y++)
                    {
                        Assert.AreEqual(
                            TerrainGenerator.SampleContent(wx, y, wz, seed, Dimension.Lestingia, 11),
                            blocks[ChunkData.IndexOf(lx, y, lz)],
                            $"ChunkJob mismatch at ({wx},{y},{wz})");
                    }
                }
            }
        }

        [Test]
        public void ProceduralTownsCliffWaterfallsVillagersAndAbundantWildlifePopulateWorld()
        {
            const int seed = 2506;
            // Guaranteed Frontier Town at (-188, 52)
            Assert.IsTrue(RealmWondersPlan.TryGetTownCenter(-1, 0, seed, out int tx, out int tBaseY, out int tz, out _));
            Assert.AreEqual(-188, tx);
            Assert.AreEqual(52, tz);
            // Central town fountain spring & lantern
            Assert.AreEqual(BlockRegistry.WaterId, TerrainGenerator.SampleContent(tx + 1, tBaseY + 1, tz, seed, Dimension.Lestingia, 11));
            Assert.AreEqual(56, TerrainGenerator.SampleContent(tx, tBaseY + 6, tz, seed, Dimension.Lestingia, 11));

            // Guaranteed Cliff Waterfall at (-84, -232): cascading water face + turquoise plunge lagoon
            Assert.IsTrue(RealmWondersPlan.TryGetWonderSite(-1, -2, seed, out int wx, out int wy, out int wz, out int wkind));
            Assert.AreEqual(1, wkind);
            Assert.AreEqual(BlockRegistry.WaterId, TerrainGenerator.SampleContent(wx, wy + 8, wz + 1, seed, Dimension.Lestingia, 11), "Cliff waterfall cascade");
            Assert.AreEqual(BlockRegistry.WaterId, TerrainGenerator.SampleContent(wx, wy, wz + 8, seed, Dimension.Lestingia, 11), "Cliff waterfall plunge lagoon");

            // LivingSimulation spawns bustling townsfolk + cats in procedural towns & prevents duplicate Kingdom spawns
            var edits = new Dictionary<Vector3Int, ushort>();
            var sim = new LivingSimulation(
                seed,
                new LivingSave(),
                (x, y, z) => edits.TryGetValue(new Vector3Int(x, y, z), out var b) ? b : TerrainGenerator.SampleContent(x, y, z, seed, Dimension.Lestingia, 11),
                (x, y, z, b) => edits[new Vector3Int(x, y, z)] = b,
                11);

            sim.Step(0.1f, new Vector3(tx, tBaseY + 2, tz), true, false);
            Assert.GreaterOrEqual(sim.Actors.Count, 10, "Procedural town should spawn bustling citizens and town cats");

            // Stepping twice near Kingdom must NOT duplicate v:kingdom:* citizens
            sim.Step(0.1f, new Vector3(KingdomPlan.CenterX, 65, KingdomPlan.CenterZ), true, false);
            int afterFirstKingdomVisit = sim.Actors.Count;
            sim.Step(0.1f, new Vector3(KingdomPlan.CenterX, 65, KingdomPlan.CenterZ), true, false);
            Assert.AreEqual(afterFirstKingdomVisit, sim.Actors.Count, "Kingdom citizens must spawn once per site, never leaking duplicates");

            // WildlifeSimulation v11 spawns abundant herds + soaring SkyWhales with positive flightHeight
            var wild = new WildlifeSimulation(seed, null, 11)
            {
                SurfaceAt = (x, z) => 3,
                SkySurfaceAt = (x, z) => RealmWondersPlan.TryGetSkySurface(x, z, seed, out int sy) ? sy : -1
            };
            wild.Spawn(
                new[] { new AnimalObserver { id = "p1", x = -148f, y = 70f, z = -72f } },
                (gx, gz, prev, r, h) => prev > 100f ? prev : 64f);
            Assert.GreaterOrEqual(wild.Animals.Count, 40, "v11 wildlife herds should richly populate valleys and sky islands");
        }

        [Test]
        public void ProfileAvatarsXpFormulaAndExactMainMenuLayoutWork()
        {
            // 1. All 12 procedural hero avatars generate valid 64x64 sprites
            Assert.AreEqual(12, ProfileAvatar.Catalog.Length);
            foreach (var arch in ProfileAvatar.Catalog)
            {
                var sprite = ProfileAvatar.GetSprite(arch.Id);
                Assert.IsNotNull(sprite, "Missing sprite for avatar " + arch.Id);
                Assert.AreEqual(64, sprite.texture.width);
                Assert.AreEqual(64, sprite.texture.height);
            }

            // 2. Exact closed-form XP progress math matches server levelFor(xp)
            Assert.AreEqual(0, AccountClient.XpFloorForLevel(1));
            Assert.AreEqual(200, AccountClient.XpFloorForLevel(2));
            Assert.AreEqual(600, AccountClient.XpFloorForLevel(3));
            Assert.AreEqual(0.5f, AccountClient.XpProgress01(100, 1), 0.001f);

            // 3. Main Menu exact layout: Balance top-right, Avatar + Nickname card above the 4 requested buttons!
            var owner = new GameObject("Menu Layout Test Owner");
            owner.SetActive(false);
            var uiGo = new GameObject("Menu Layout UI");
            var prevEventSystem = EventSystem.current;
            try
            {
                var game = owner.AddComponent<PrototypeGame>();
                VisualSettings.Load();
                var ui = uiGo.AddComponent<GameUI>();
                ui.Initialize(game);

                var balance = FindButton(ui, "Home.Balance");
                var profile = FindButton(ui, "Home.Account");
                var single = FindButton(ui, "Home.Play");
                var multi = FindButton(ui, "Home.Coop");
                var settings = FindButton(ui, "Home.Settings");
                var friends = FindButton(ui, "Home.Friends");
                var quit = FindButton(ui, "Home.Quit");

                Assert.IsNotNull(balance, "Top-right Balance button must exist");
                Assert.IsNotNull(profile, "Avatar + Nickname profile card must exist");
                Assert.IsNotNull(single, "Singleplayer button must exist");
                Assert.IsNotNull(multi, "Multiplayer button must exist");
                Assert.IsNotNull(settings, "Settings button must exist");
                Assert.IsNotNull(friends, "Friends button must exist");
                Assert.IsNotNull(quit, "Quit button must exist");

                // Verify Russian labels requested by user
                StringAssert.Contains("Одиночная игра", CollectText(single));
                StringAssert.Contains("Многопользовательская игра", CollectText(multi));
                StringAssert.Contains("Настройки", CollectText(settings));
                StringAssert.Contains("Друзья", CollectText(friends));
                StringAssert.Contains("БАЛАНС", CollectText(balance));
                StringAssert.Contains("@", CollectText(profile));

                // Verify spatial ordering (top-down uses negative anchoredPosition.y from top-left anchor)
                var balRt = balance.GetComponent<RectTransform>();
                var profRt = profile.GetComponent<RectTransform>();
                var playRt = single.GetComponent<RectTransform>();
                var coopRt = multi.GetComponent<RectTransform>();
                var setRt = settings.GetComponent<RectTransform>();
                var frdRt = friends.GetComponent<RectTransform>();

                Assert.Greater(balRt.anchoredPosition.x, 1100f, "Balance widget must be in top-right corner");
                Assert.Greater(balRt.anchoredPosition.y, -90f, "Balance widget must be near top edge");

                // Avatar + Nickname card is directly ABOVE all 4 main buttons (less negative Y = higher on screen)
                Assert.Greater(profRt.anchoredPosition.y, playRt.anchoredPosition.y, "Avatar + Nickname must be above Одиночная игра");
                Assert.Greater(playRt.anchoredPosition.y, coopRt.anchoredPosition.y, "Одиночная игра above Многопользовательская игра");
                Assert.Greater(coopRt.anchoredPosition.y, setRt.anchoredPosition.y, "Многопользовательская игра above Настройки");
                Assert.Greater(setRt.anchoredPosition.y, frdRt.anchoredPosition.y, "Настройки above Друзья");

                // Verify navigation of new buttons
                friends.onClick.Invoke();
                Assert.AreEqual("Friends", ui.ScreenName);
                ui.ShowHome();
                profile = FindButton(ui, "Home.Account");
                profile.onClick.Invoke();
                Assert.AreEqual("Account", ui.ScreenName);
                ui.ShowHome();
                balance = FindButton(ui, "Home.Balance");
                balance.onClick.Invoke();
                Assert.AreEqual("Store", ui.ScreenName);
            }
            finally
            {
                Object.DestroyImmediate(uiGo);
                Object.DestroyImmediate(owner);
                if (!prevEventSystem && EventSystem.current)
                    Object.DestroyImmediate(EventSystem.current.gameObject);
            }
        }

        static Button FindButton(GameUI ui, string name)
        {
            foreach (var b in ui.GetComponentsInChildren<Button>())
                if (b.name == name) return b;
            return null;
        }

        static string CollectText(Component root)
        {
            var list = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Text>())
                list.Add(t.text);
            return string.Join(" | ", list);
        }
    }
}
