using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Runtime;
using LestingCraft.World;
using LestingCraft.Presentation;

namespace LestingCraft.Tests
{
    public sealed class EraV9RegressionTests
    {
        [Test]
        public void NullInventorySlotsSurviveGive666RadiationStepAndThermonukeDetonation()
        {
            // Reproduce legacy/restored save state: Inventory exists, but its serialized slot array is null.
            var inventory = new Inventory { slots = null };
            var grant = ChatCommands.Execute("/give 666", false, true, inventory, 2506, Vector3.zero, 1);
            Assert.AreEqual("thermonuke", grant.Action, grant.Message);
            Assert.AreEqual(1, inventory.Count(Items.ThermonukeId), "Survival /give 666 must repair slots and grant the secret warhead.");

            var damagedInventory = new Inventory { slots = null };
            var radiationSimulation = new IndustrialAndRadiaSimulation();
            bool requestRadia;
            radiationSimulation.StepPowerAndRadiation(
                0.25f, Vector3.zero, Dimension.Lestingia, 1f, damagedInventory,
                (x, y, z) => (ushort)0, out requestRadia);
            Assert.AreEqual(0, damagedInventory.Count(Items.ThermonukeId), "Radiation sampling must tolerate and repair null inventory slots.");
            Assert.IsFalse(requestRadia);

            var worldObject = new GameObject("ThermonukeRegressionWorld");
            var playerObject = new GameObject("ThermonukeRegressionPlayer");
            var sessionObject = new GameObject("ThermonukeRegressionSession");
            var runtimeObject = new GameObject("ThermonukeRegressionRuntime");
            try
            {
                var world = worldObject.AddComponent<VoxelWorld>();
                typeof(VoxelWorld).GetProperty("ContentVersion", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .GetSetMethod(true).Invoke(world, new object[] { 9 });

                var player = playerObject.AddComponent<FirstPersonController>();
                player.transform.position = new Vector3(77.5f, 78f, 19.5f);
                var session = sessionObject.AddComponent<SurvivalSession>();
                var save = new SessionSave { seed = 2506, mode = (int)GameMode.Survival };
                save.player.inventory = inventory;
                typeof(SurvivalSession).GetProperty("Data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .GetSetMethod(true).Invoke(session, new object[] { save });

                // Seed a solid epicenter and a point on the outer blast liner.
                world.Edit(34, 49, 40, BlockRegistry.HazardSteelId, false);
                world.Edit(46, 49, 40, BlockRegistry.HazardSteelId, false);
                var runtime = runtimeObject.AddComponent<IndustrialTechRuntime>();
                runtime.ArmExplosive(new Vector3(34.5f, 49.5f, 40.5f), true);

                // This real runtime step runs radiation sampling, the 8-second fuse and DetonateCharge.
                bool requestEntry, requestExit;
                runtime.StepRuntime(8.1f, world, player, session, null, 0f, 0f, 0f, 0f, 0f,
                    out requestEntry, out requestExit);

                Assert.IsFalse(session.Vitals.dead, "A player about 56m from the epicenter must survive the 38m blast-kill radius.");
                Assert.AreEqual(BlockRegistry.AirId, world.GetBlock(34, 49, 40), "The nuke must excavate its crater.");
                Assert.AreEqual(BlockRegistry.TrinititeSlagId, world.GetBlock(46, 49, 40), "The outer liner must vitrify into Trinitite.");
                Assert.AreEqual(1, runtime.Sim.FalloutZones.Count, "DetonateCharge must create a fallout zone for ID 666.");
                StringAssert.Contains("ОБЪЕКТА 666", session.Notice);
                Assert.AreEqual(1, session.Inventory.Count(Items.ThermonukeId), "Session.Inventory must remain non-null and usable through detonation.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeObject);
                UnityEngine.Object.DestroyImmediate(sessionObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(worldObject);
            }
        }

        [Test]
        public void RadiaAtmosphereDarkensFloodWaterAndRestoresReflectionOutsideTheDimension()
        {
            var radia = AtmosphereLighting.At(760.0, Dimension.Radia, 0.94f, 1f);
            var earth = AtmosphereLighting.At(760.0, Dimension.Lestingia, 0.28f, 0f);
            Assert.AreEqual(0.12f, radia.WaterReflection, 0.0001f,
                "Cherenkov basin water must not mirror the bright sky; blue underwater rows should remain legible.");
            Assert.AreEqual(1f, earth.WaterReflection, 0.0001f,
                "Leaving Radia must restore normal water reflections for ordinary worlds.");
        }
    }
}
