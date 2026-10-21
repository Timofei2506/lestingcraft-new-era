using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Networking;
using LestingCraft.Presentation;
using LestingCraft.Runtime;
using LestingCraft.Story;
using LestingCraft.World;

namespace LestingCraft.Tests
{
    public sealed class EraExpansionTests
    {
        [Test]
        public void Interactive3DCockpitMouseRaycastsSwitchesCoverThrottleStickAndMfd()
        {
            var host = new GameObject("ShuttleCabinHost");
            try
            {
                // Build the FULL physical orbiter cabin (carved hull MeshCollider + seats + restraints + displays + 3D cockpit rig)
                // and shoot all 16 rays directly from the actual seated Pilot Seat 0 eye position!
                var cabin = VerticalShuttleInteriorVisual.Create(host.transform);
                var helm = new OpenSpaceHelm();
                helm.EnterOrbitAround(CelestialBodyId.Earth);
                cabin.BindHelm(helm);
                var rig = cabin.InteractiveRig;
                var panel = cabin.Panel;
                Vector3 pilotEye = cabin.transform.TransformPoint(CockpitInteractiveRig.PilotEyeLocal);

                Assert.AreEqual(16, rig.Controls.Count, "All 16 physical 3D cockpit controls (direct flight, zero Time Warp buttons) must register BoxCollider hitboxes");
                foreach (var kv in rig.Controls)
                {
                    var box = kv.Value.GetComponent<BoxCollider>();
                    Assert.IsNotNull(box, "Missing BoxCollider on " + kv.Key);
                    if (kv.Key == CockpitControlId.MasterArmSwitch)
                    {
                        Assert.IsFalse(box.enabled, "Guarded MasterArmSwitch must be physically shielded while safety cover is closed");
                        continue;
                    }
                    Vector3 worldCenter = kv.Value.transform.TransformPoint(box.center);
                    var ray = new Ray(pilotEye, (worldCenter - pilotEye).normalized);
                    rig.StepMouseInteraction(ray, false, false, false, Vector2.zero, 0f);
                    Assert.AreEqual(kv.Key, rig.HoveredControl, "Seated pilot 3D mouse raycast must cleanly hit " + kv.Key + " without cabin occlusion");
                }

                // Full preflight sequence using ONLY seated-pilot 3D mouse clicks on cockpit switches!
                Assert.IsFalse(panel.MainBus);
                Click3DControl(rig, CockpitControlId.MainBusSwitch, pilotEye);
                Assert.IsTrue(panel.MainBus);

                Click3DControl(rig, CockpitControlId.FuelFeedSwitch, pilotEye);
                Assert.IsTrue(panel.FuelFeed);

                Click3DControl(rig, CockpitControlId.HatchSwitch, pilotEye);
                Assert.IsTrue(panel.HatchSealed);

                Assert.IsFalse(rig.SafetyCoverOpen);
                Click3DControl(rig, CockpitControlId.MasterArmCover, pilotEye);
                Assert.IsTrue(rig.SafetyCoverOpen);

                Click3DControl(rig, CockpitControlId.MasterArmSwitch, pilotEye);
                Assert.IsTrue(panel.Arm);

                var ignite = Click3DControl(rig, CockpitControlId.IgniteButton, pilotEye);
                Assert.IsTrue(ignite.RequestedIgnite);

                // Scroll wheel & drag on 3D Throttle Lever + 3D Time Warp buttons + MFD Galaxy jump
                float beforeThrottle = helm.Throttle;
                Scroll3DControl(rig, CockpitControlId.ThrottleLever, 0.25f, pilotEye);
                Assert.Greater(helm.Throttle, beforeThrottle);

                // Direct real-time spaceflight: Time Warp speed-up buttons are removed from 3D cockpit
                Assert.AreEqual(1.0, helm.TimeWarp, 1e-6);
                Assert.IsFalse(rig.Controls.ContainsKey(CockpitControlId.TimeWarpUpButton));
                Assert.IsFalse(rig.Controls.ContainsKey(CockpitControlId.TimeWarpDownButton));

                var galSwitch = Click3DControl(rig, CockpitControlId.MfdToggleGalaxy, pilotEye);
                Assert.IsTrue(galSwitch.RequestedHyperjump);
                helm.Step(0.1f, 0f, 0f, 0f);
                for (int i = 0; i < 20; i++) helm.Step(0.1f, 0f, 0f, 0f);
                Assert.AreEqual(GalaxyId.Andromeda, helm.CurrentGalaxy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        static CockpitActionResult Click3DControl(CockpitInteractiveRig rig, CockpitControlId id, Vector3 pilotEye)
        {
            var hb = rig.Controls[id];
            var box = hb.GetComponent<BoxCollider>();
            Vector3 target = hb.transform.TransformPoint(box.center);
            return rig.StepMouseInteraction(new Ray(pilotEye, (target - pilotEye).normalized), true, true, false, Vector2.zero, 0f);
        }

        static void Scroll3DControl(CockpitInteractiveRig rig, CockpitControlId id, float scroll, Vector3 pilotEye)
        {
            var hb = rig.Controls[id];
            var box = hb.GetComponent<BoxCollider>();
            Vector3 target = hb.transform.TransformPoint(box.center);
            rig.StepMouseInteraction(new Ray(pilotEye, (target - pilotEye).normalized), false, false, false, Vector2.zero, scroll);
        }

        [Test]
        public void FullSolarSystemSevenStarSystemsTwoGalaxiesAndManualOpenSpaceFlight()
        {
            Assert.AreEqual(2, CelestialAtlas.GalaxyNames.Length);
            Assert.AreEqual(7, CelestialAtlas.Systems.Length);
            Assert.AreEqual(24, CelestialAtlas.Bodies.Length);

            // Verify every major body of the Solar System + Rings + Extrasolar + Andromeda M31
            string[] mustHave =
            {
                "sun", "mercury", "venus", "earth", "station", "moon", "mars", "ceres",
                "jupiter", "europa", "saturn", "titan", "uranus", "neptune", "pluto",
                "proxima-b", "trappist-1d", "trappist-1e", "sirius-krystallos", "kepler-452b",
                "andromeda-prime", "mirach-citadel", "somnia"
            };
            foreach (string slug in mustHave)
            {
                var id = CelestialAtlas.ParseWireKey(slug);
                Assert.AreEqual(slug, CelestialAtlas.GetBody(id).WireKey, "Missing celestial body " + slug);
            }
            Assert.IsTrue(CelestialAtlas.GetBody(CelestialBodyId.Saturn).HasRings);
            Assert.IsTrue(CelestialAtlas.GetBody(CelestialBodyId.Uranus).HasRings);
            Assert.IsTrue(CelestialAtlas.GetBody(CelestialBodyId.AndromedaPrime).HasRings);

            // Verify real 1:1 astronomical semi-major axes and hierarchical moon/station orbits!
            SpaceVector earthPos = CelestialAtlas.WorldPositionKm(CelestialBodyId.Earth, 42.0);
            SpaceVector moonPos = CelestialAtlas.WorldPositionKm(CelestialBodyId.Moon, 42.0);
            SpaceVector saturnPos = CelestialAtlas.WorldPositionKm(CelestialBodyId.Saturn, 42.0);
            SpaceVector titanPos = CelestialAtlas.WorldPositionKm(CelestialBodyId.Titan, 42.0);
            Assert.AreEqual(149598000.0, earthPos.Magnitude, 5e4, "Earth orbits Sol at 1 AU (149.598 Mkm)");
            Assert.AreEqual(1433530000.0, saturnPos.Magnitude, 6e6, "Saturn orbits Sol at ~9.58 AU (1.4335 Bkm)");
            Assert.AreEqual(384400.0, SpaceVector.Distance(earthPos, moonPos), 1500.0, "Moon must orbit Earth at real 384,400 km");
            Assert.AreEqual(1221870.0, SpaceVector.Distance(saturnPos, titanPos), 5000.0, "Titan must orbit Saturn at real 1,221,870 km");

            // Real manual 6-DOF piloting in continuous direct cruise (zero Time Warp buttons, ~33.4 min Earth -> Moon)
            var helm = new OpenSpaceHelm();
            helm.EnterOrbitAround(CelestialBodyId.Earth);
            SpaceVector p0 = helm.PositionKm;
            helm.SetManualControl(true);
            helm.SetThrottle(0.85f);
            helm.Boost = true;
            for (int i = 0; i < 15; i++) helm.Step(0.1f, 0.35f, -0.25f, 0.15f);
            double realTimeMovedKm = SpaceVector.Distance(p0, helm.PositionKm);
            Assert.Greater(realTimeMovedKm, 8.0, "Ship accelerates smoothly under continuous thrust");
            Assert.Less(realTimeMovedKm, 320.0, "Ship stays calibrated to ~185 km/s lunar cruise speed");

            // Time Warp speed-ups stay strictly locked at 1.0x even if legacy methods are called
            for (int w = 0; w < 7; w++) helm.IncreaseTimeWarp();
            Assert.AreEqual(1.0, helm.TimeWarp, 1e-6, "Direct flight keeps TimeWarp locked at 1.0x");
            helm.ResetTimeWarp();

            helm.StartHyperjump(CelestialBodyId.AndromedaPrime);
            for (int i = 0; i < 20; i++) helm.Step(0.1f, 0f, 0f, 0f);
            Assert.AreEqual(GalaxyId.Andromeda, helm.CurrentGalaxy);
            Assert.AreEqual(StarSystemId.AndromedaCore, helm.CurrentSystem);
            Assert.IsTrue(helm.InLandingRange, "Hyperjump places ship directly on final landing orbit");

            // Verify every SpaceFlightVisuals celestial body uses crisp 24-vertex voxel cubes & square rings (zero spheres)
            var visHost = new GameObject("CubicSpaceVisualTest");
            try
            {
                var visuals = SpaceFlightVisuals.Ensure(visHost.transform);
                visuals.SetOpenSpaceFlightActive(helm, null, visHost.transform);
                Assert.AreEqual(24, visuals.SpawnedAtlasBodyCount);
                foreach (var mf in visuals.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    StringAssert.DoesNotContain("Sphere", mf.sharedMesh.name, "Space body must be cubic voxel geometry, never spherical");
                    if (mf.gameObject.name.StartsWith("Atlas_"))
                        Assert.AreEqual(24, mf.sharedMesh.vertexCount, "Atlas body " + mf.gameObject.name + " must be a crisp 6-face 24-vertex voxel cube");
                    if (mf.gameObject.name.StartsWith("Rings_"))
                        Assert.AreEqual(24, mf.sharedMesh.vertexCount, "Planetary rings " + mf.gameObject.name + " must be a crisp 3D square voxel ring plate");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(visHost);
            }
        }

        [Test]
        public void CreepyDreamDimensionEnforcesSleepParalysisStalkerGazeTotalIsolationAndZeroInteraction()
        {
            var dream = new DreamAnomaly();
            Assert.IsFalse(dream.IsActive);
            Assert.IsTrue(dream.AllowsWorldOrItemInteraction);
            Assert.IsFalse(dream.IsolatesFromMultiplayer);

            // Deterministic rare trigger check across night sleep seeds
            int triggered = 0;
            for (int s = 0; s < 200; s++)
                if (DreamAnomaly.RollDreamTrigger(2506, s, DreamAnomaly.ComputeSleepDreamProbability(20f, 25f, true)))
                    triggered++;
            Assert.Greater(triggered, 15);
            Assert.Less(triggered, 90, "Dream entry must remain a genuinely rare, eerie surprise");

            // Phase 1: Sleep paralysis immobilizes body, locks all world/item interactions, and isolates multiplayer
            dream.EnterDream(Dimension.Lestingia, 80.5f, 45.2f, 38.5f);
            Assert.AreEqual(DreamPhase.SleepParalysis, dream.Phase);
            Assert.IsTrue(dream.IsParalyzed);
            Assert.IsFalse(dream.CanWalkInDream);
            Assert.IsFalse(dream.AllowsWorldOrItemInteraction);
            Assert.IsTrue(dream.IsolatesFromMultiplayer);

            // Advance past 6.5s sleep paralysis -> Phase 2: eerie walk through pale monolith colonnade
            for (int i = 0; i < 68; i++) dream.Step(0.1f, new Vector3(0.5f, 45.15f, 0.5f), Vector3.forward);
            Assert.AreEqual(DreamPhase.NightmareWalk, dream.Phase);
            Assert.IsFalse(dream.IsParalyzed);
            Assert.IsTrue(dream.CanWalkInDream);
            Assert.IsFalse(dream.AllowsWorldOrItemInteraction, "Even while walking in Dream, block/item interaction stays 100% forbidden");
            Assert.IsTrue(dream.IsolatesFromMultiplayer, "Even while walking in Dream, multiplayer stays 100% isolated");

            // Weeping-shadow watcher freezes when stared at directly and creeps closer when player looks away!
            Vector3 playerPos = new Vector3(0.5f, 45.15f, 4.0f);
            Vector3 lookDirect = (dream.StalkerPosition - playerPos).normalized;
            float zFrozen = dream.StalkerZ;
            dream.Step(0.1f, playerPos, lookDirect);
            Assert.AreEqual(zFrozen, dream.StalkerZ, 1e-4f, "Shadow watcher freezes completely inside player gaze");

            dream.Step(0.1f, playerPos, -lookDirect);
            Assert.AreNotEqual(zFrozen, dream.StalkerZ, "Shadow watcher advances silently when player turns away");

            // Reaching the Pale Threshold Arch (z = 46) wakes the dreamer back into their real bed!
            dream.Step(0.1f, new Vector3(0f, 45.15f, 46f), Vector3.forward);
            Assert.AreEqual(DreamPhase.WakingGasp, dream.Phase);
            bool woke = false;
            for (int i = 0; i < 20; i++) woke |= dream.Step(0.1f, new Vector3(0f, 45.15f, 46f), Vector3.forward);
            Assert.IsTrue(woke);
            Assert.AreEqual(DreamPhase.Awake, dream.Phase);
            Assert.IsTrue(dream.AllowsWorldOrItemInteraction);
            Assert.IsFalse(dream.IsolatesFromMultiplayer);
        }

        [Test]
        public void ThreeChapterSagaCodexBedsUpgradedVillagesAndRealLanternTorchStreetPoles()
        {
            Assert.AreEqual(7, StoryChapters.Entries.Length);
            int mask = 0;
            CollectionAssert.IsNotEmpty(StoryChapters.EvaluateProgressionTransmission(Dimension.Moon, GalaxyId.MilkyWay, 0, false, ref mask));
            CollectionAssert.IsNotEmpty(StoryChapters.EvaluateProgressionTransmission(Dimension.Lestingia, GalaxyId.MilkyWay, 1, false, ref mask));
            CollectionAssert.IsNotEmpty(StoryChapters.EvaluateProgressionTransmission(Dimension.AndromedaPrime, GalaxyId.Andromeda, 1, false, ref mask));
            CollectionAssert.IsNotEmpty(StoryChapters.EvaluateProgressionTransmission(Dimension.AndromedaPrime, GalaxyId.Andromeda, 1, true, ref mask));
            Assert.AreEqual(15, mask);

            // Craftable Bed (55), Torch (54), Lantern (56), Bookshelf (57), CarvedOak (58), Shingles (59), TitaniumHull (66)
            var craftedOutputs = new HashSet<int>();
            foreach (var r in CraftingTable.All) craftedOutputs.Add(r.Output);
            foreach (int mustCraft in new[] { 54, 55, 56, 57, 58, 59, 66 })
                Assert.IsTrue(craftedOutputs.Contains(mustCraft), "Missing crafting recipe for item/block " + mustCraft);

            // Verify street poles in Silent Ford carry real Lanterns (56) / Torches (54) instead of raw Campfires (15)!
            int baseY = StoryWorld.Floor(2506);
            StoryWorld.TrySample(88, baseY + 5, 48, 2506, out ushort oldPole7, 7);
            StoryWorld.TrySample(88, baseY + 4, 48, 2506, out ushort newPoleHead8, 8);
            StoryWorld.TrySample(88, baseY + 5, 48, 2506, out ushort newPoleTop8, 8);
            Assert.AreEqual(15, oldPole7, "v7 compatibility preserved");
            Assert.AreEqual(BlockRegistry.CarvedOakId, newPoleHead8, "Carved oak cross-head on v8 street lamp post");
            Assert.AreEqual(BlockRegistry.LanternBlockId, newPoleTop8, "Real glowing Lantern replaces campfire on v8 street lamp post");

            // Verify furnished villager house in Silent Ford contains real Bed (55), Bookshelf (57), and Porch Torch (54)
            StoryWorld.TrySample(80, baseY + 1, 38, 2506, out ushort cottageBed, 8);
            StoryWorld.TrySample(84, baseY + 1, 34, 2506, out ushort cottageShelf, 8);
            StoryWorld.TrySample(82, baseY + 2, 32, 2506, out ushort cottageTorch, 8);
            Assert.AreEqual(BlockRegistry.BedBlockId, cottageBed);
            Assert.AreEqual(BlockRegistry.BookshelfId, cottageShelf);
            Assert.AreEqual(BlockRegistry.TorchBlockId, cottageTorch);
        }

        [Test]
        public void DiverseVillagersTwentyAnimalsFiftyOneExpandedBlocksAndTwelveBiomes()
        {
            Assert.AreEqual(14, GameRules.Version);
            Assert.AreEqual(71, ExpandedContent.Blocks.Length);
            Assert.AreEqual(89, ExpandedContent.MaxBlock);
            Assert.AreEqual(112, SurfaceLayers.Names.Length);
            Assert.AreEqual(12, BiomeGenerator.Names.Length);
            Assert.AreEqual(30, WildlifeSimulation.SpeciesCount);
            Assert.GreaterOrEqual(TradeOffer.ProfessionCount, 10);

            // All 12 biomes appear deterministically in v8 while v7 remains strictly 6 biomes
            var seen7 = new HashSet<BiomeKind>();
            var seen8 = new HashSet<BiomeKind>();
            for (int x = -2400; x <= 2400; x += 96)
                for (int z = -2400; z <= 2400; z += 96)
                {
                    if (ExpeditionGenerator.Reserved(x, z)) continue;
                    seen7.Add(BiomeGenerator.At(x, z, 2506, 7));
                    seen8.Add(BiomeGenerator.At(x, z, 2506, 8));
                }
            Assert.AreEqual(6, seen7.Count);
            Assert.AreEqual(12, seen8.Count);

            // Diverse villagers across all 10 professions build clean multi-part meshes with valid normals and zero child colliders
            for (int i = 0; i < 12; i++)
            {
                int job = i % TradeOffer.ProfessionCount;
                var offer = TradeOffer.ForJob(job);
                Assert.Greater(offer.InputCount, 0);
                Assert.Greater(offer.OutputCount, 0);
                var go = new GameObject("VillagerTest_" + i);
                try
                {
                    var look = SettlerAppearance.For("villager-" + i, job);
                    Assert.GreaterOrEqual(look.Coat, 0);
                    go.AddComponent<SettlerVisual>().Build(0, job, "villager-" + i);
                    Assert.AreEqual(0, go.GetComponentsInChildren<Collider>(true).Length);
                    var filters = go.GetComponentsInChildren<MeshFilter>(true);
                    Assert.Greater(filters.Length, 6);
                    foreach (var mf in filters)
                    {
                        Assert.IsNotNull(mf.sharedMesh);
                        Assert.GreaterOrEqual(mf.sharedMesh.vertexCount, 24);
                        Assert.Greater(mf.sharedMesh.normals.Length, 0);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            // All 20 wildlife species (0..19) build real multi-part rigs, distinct coats, and flying/swimming/terrestrial stats
            var coats = new HashSet<int>();
            for (int s = 0; s < 20; s++)
            {
                Assert.Greater(WildlifeSimulation.MaxHealth(s), 0);
                coats.Add(AnimalVisual.FurLayerForSpecies(s));
                var animalGo = new GameObject("AnimalTest_" + s);
                try
                {
                    var rig = animalGo.AddComponent<AnimalVisual>();
                    rig.Build(s);
                    Assert.GreaterOrEqual(animalGo.GetComponentsInChildren<MeshFilter>(true).Length, 6);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(animalGo);
                }
            }
            Assert.AreEqual(20, coats.Count, "Every one of the 20 animal species must map to its own dedicated PBR coat");

            // Distinct planetary atmospheres (Titan != Mars != Venus != Moon != AndromedaPrime) & denser v8 Earth herds
            var marsAtmo = AtmosphereLighting.At(600.0, Dimension.Mars, 0.2f, 0f);
            var titanAtmo = AtmosphereLighting.At(600.0, Dimension.Titan, 0.2f, 0f);
            var moonAtmo = AtmosphereLighting.At(600.0, Dimension.Moon, 0f, 0f);
            float zenithDiff = Mathf.Abs(marsAtmo.Zenith.r - titanAtmo.Zenith.r)
                             + Mathf.Abs(marsAtmo.Zenith.g - titanAtmo.Zenith.g)
                             + Mathf.Abs(marsAtmo.Zenith.b - titanAtmo.Zenith.b);
            Assert.Greater(zenithDiff, 0.35f, "Titan zenith must be teal-cyan, NOT Mars dusty orange!");
            Assert.Less(moonAtmo.Zenith.r + moonAtmo.Zenith.g + moonAtmo.Zenith.b, 0.05f, "Airless Moon zenith must be vacuum black");
            Assert.Greater(AtmosphereLighting.CloudCoverageForRealm(Dimension.Venus, 0.5f), 0.85f, "Venus must maintain thick golden-amber cloud cover");
            Assert.AreEqual(0f, AtmosphereLighting.CloudCoverageForRealm(Dimension.Moon, 0.5f), 1e-4f, "Airless Moon must have zero clouds");

            WildlifeSimulation.Ground meadowGround = (float gx, float gz, float prevY, float rad, float hgt) => 68f;
            var obs = new AnimalObserver { x = 0f, z = 0f, alive = true };
            var simV7 = new WildlifeSimulation(2506, null, 7);
            var simV8 = new WildlifeSimulation(2506, null, 8);
            simV7.Spawn(obs, meadowGround);
            simV8.Spawn(obs, meadowGround);
            Assert.GreaterOrEqual(simV8.Animals.Count, simV7.Animals.Count * 3, "Earth v8 biome herds must spawn >3x more wildlife than legacy v7");
        }
    }
}
