using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.Runtime;

namespace LestingCraft.Tests
{
    public sealed class EraV9ExpansionTests
    {
        [Test]
        public void DirectMoonCruiseTakesThirtyToThirtySevenMinutesWithZeroTimeWarp()
        {
            var helm = new OpenSpaceHelm();
            helm.EnterOrbitAround(CelestialBodyId.Earth);
            helm.SelectTarget(CelestialBodyId.Moon);

            double distKm = helm.DistanceToTargetKm();
            Assert.Greater(distKm, 355000.0);
            Assert.Less(distKm, 395000.0);

            // Time Warp is strictly locked at 1.0x even when legacy warp calls are invoked
            helm.IncreaseTimeWarp();
            helm.IncreaseTimeWarp();
            Assert.AreEqual(1.0, helm.TimeWarp, 1e-6);

            // Direct cruise duration from Earth orbit to Moon at MaxSublightSpeedKmS (185 km/s)
            double cruiseMinutes = (distKm / OpenSpaceHelm.MaxSublightSpeedKmS) / 60.0;
            Assert.GreaterOrEqual(cruiseMinutes, 30.0, "Earth -> Moon direct cruise must be at least 30 minutes");
            Assert.LessOrEqual(cruiseMinutes, 37.0, "Earth -> Moon direct cruise must be at most 37 minutes (~33.4 min)");
        }

        [Test]
        public void SecretThermonuke666IsStrictlyUnobtainableEverywhereExceptGive666()
        {
            Assert.IsTrue(Items.Exists(Items.ThermonukeId));
            Assert.Less(Array.IndexOf(Items.CatalogIds, Items.ThermonukeId), 0, "666 must NOT exist in Items.CatalogIds");
            Assert.Less(Array.IndexOf(ItemArtwork.AllIds, Items.ThermonukeId), 0, "666 must NOT exist in ItemArtwork.AllIds");
            Assert.AreEqual(Items.CatalogIds.Length, ItemArtwork.AllIds.Length, "Catalog parity invariant must hold");

            // Blocked in Creative Catalog search & Take
            foreach (int id in CreativeCatalog.Query("", 0))
                Assert.AreNotEqual(Items.ThermonukeId, id, "666 must NEVER appear in Creative Catalog");
            foreach (int id in CreativeCatalog.Query("666", 0))
                Assert.AreNotEqual(Items.ThermonukeId, id, "666 search must NEVER expose secret item in Creative Catalog");
            foreach (int id in CreativeCatalog.Query("ядер", 0))
                Assert.AreNotEqual(Items.ThermonukeId, id);

            var inv = new Inventory();
            inv.Normalize();
            Assert.IsFalse(CreativeCatalog.Take(inv, Items.ThermonukeId, true, true, true), "CreativeCatalog.Take must block 666");
            Assert.AreEqual(0, inv.Count(Items.ThermonukeId));

            // Blocked in every Survival Crafting recipe & Villager Trade
            foreach (var r in CraftingTable.All)
                Assert.AreNotEqual(Items.ThermonukeId, r.Output, "No recipe may ever output 666");
            for (int j = 0; j < TradeOffer.ProfessionCount; j++)
                Assert.AreNotEqual(Items.ThermonukeId, TradeOffer.ForJob(j).Output, "No trade may ever output 666");

            // Unconditionally granted ONLY via /give 666 (even in pure Survival with creative = false!)
            var survInv = new Inventory();
            survInv.Normalize();
            var denied = ChatCommands.Execute("/give 76 1", false, true, survInv, 2506, Vector3.zero, 1);
            Assert.AreEqual(0, survInv.Count(76), denied.Message);

            var granted = ChatCommands.Execute("/give 666", false, true, survInv, 2506, Vector3.zero, 1);
            Assert.AreEqual(1, survInv.Count(Items.ThermonukeId), granted.Message);
        }

        [Test]
        public void RealElectricalGridDosimetryLeadShieldingAndRadiaBossEscapeWorkDeterministically()
        {
            var blocks = new Dictionary<Vector3Int, ushort>();
            Func<int, int, int, ushort> sample = (x, y, z) =>
            {
                ushort b;
                return blocks.TryGetValue(new Vector3Int(x, y, z), out b) ? b : (ushort)0;
            };

            // Water-cooled VVER Nuclear Reactor (76) wired via Copper Bus Cables (72) to Battery (73), Floodlight (77) & Powered Rail (86)
            blocks[new Vector3Int(-1, 50, 0)] = BlockRegistry.WaterId;
            blocks[new Vector3Int(0, 50, 0)] = BlockRegistry.NuclearReactorId;
            blocks[new Vector3Int(1, 50, 0)] = BlockRegistry.CopperCableId;
            blocks[new Vector3Int(2, 50, 0)] = BlockRegistry.BatteryBankId;
            blocks[new Vector3Int(3, 50, 0)] = BlockRegistry.CopperCableId;
            blocks[new Vector3Int(4, 50, 0)] = BlockRegistry.ElectricFloodlightId;
            blocks[new Vector3Int(4, 49, 0)] = BlockRegistry.PoweredRailId;

            var sim = new IndustrialAndRadiaSimulation();
            bool transport;
            sim.StepPowerAndRadiation(1.0f, new Vector3(0f, 50f, 0f), Dimension.Lestingia, 1.0f, null, sample, out transport);
            Assert.IsTrue(sim.Grid.Powered, "Water-cooled nuclear reactor must power connected grid");
            Assert.GreaterOrEqual(sim.Grid.GenerationKw, 450f);
            Assert.Greater(sim.Grid.StoredKwh, 0f);
            Assert.IsFalse(sim.Grid.Overheating);
            Assert.IsTrue(sim.EnergizedNodes.Contains(new Vector3Int(4, 49, 0)));

            // Lead Shield (81) attenuates radiation dose rate by >= 80%
            blocks.Clear();
            blocks[new Vector3Int(0, 50, 0)] = BlockRegistry.UraniumOreId;
            sim.StepPowerAndRadiation(0.25f, new Vector3(0.5f, 50f, 3.5f), Dimension.Lestingia, 1.0f, null, sample, out transport);
            float unshieldedMsv = sim.CurrentMillisievertsPerHour;
            Assert.Greater(unshieldedMsv, 15f);

            blocks[new Vector3Int(0, 50, 1)] = BlockRegistry.LeadShieldId;
            blocks[new Vector3Int(0, 50, 2)] = BlockRegistry.LeadShieldId;
            sim.StepPowerAndRadiation(0.25f, new Vector3(0.5f, 50f, 3.5f), Dimension.Lestingia, 1.0f, null, sample, out transport);
            Assert.Less(sim.CurrentMillisievertsPerHour, unshieldedMsv * 0.2f, "Lead shielding blocks >=80% of gamma radiation");

            // Full Radia Abyss geometry check: underwater Cherenkov blocks (78), Dam Terminal (89), and flooded basin water (7)
            Assert.AreEqual(BlockRegistry.RadiaTerminalId, TerrainGenerator.SampleContent(0, 57, 73, 2506, Dimension.Radia, 9));
            Assert.AreEqual(BlockRegistry.CherenkovBlockId, TerrainGenerator.SampleContent(-10, 48, 20, 2506, Dimension.Radia, 9));
            Assert.AreEqual(BlockRegistry.WaterId, TerrainGenerator.SampleContent(-10, 50, 20, 2506, Dimension.Radia, 9));

            // Hard multi-stage escape ritual: cannot open sluice until 6 cores + all 3 Raid Bosses are defeated + 20s purge completes
            sim.ResetRadiaEncounter();
            StringAssert.Contains("ЗАБЛОКИРОВАН", sim.TryActivateDamTerminal());
            sim.HarvestedSubmergedCores = 6;
            for (int b = 0; b < 3; b++)
            {
                Vector3 bc = sim.Bosses[b].Position + Vector3.up * 3.0f;
                var ray = new Ray(bc - Vector3.forward * 5f, Vector3.forward);
                string hitMsg;
                sim.TryHitBoss(ray, 900, out hitMsg);
            }
            Assert.AreEqual(3, sim.DefeatedBossCount);
            sim.TryActivateDamTerminal();
            Assert.IsTrue(sim.PurgeActive);
            Assert.IsFalse(sim.EscapePortalOpen);
            for (int t = 0; t < 25; t++)
                sim.StepRadiaBossesAndPurge(1.0f, new Vector3(0.5f, 58f, 70f), null);
            Assert.IsTrue(sim.EscapePortalOpen, "Escape portal opens only after surviving the full 20-second sluice purge!");
        }
    }
}
