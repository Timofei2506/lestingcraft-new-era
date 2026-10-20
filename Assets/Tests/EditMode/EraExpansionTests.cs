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
            var root = new GameObject("CabinRoot");
            try
            {
                var panel = new CockpitPanel();
                var helm = new OpenSpaceHelm();
                helm.EnterOrbitAround(CelestialBodyId.Earth);
                var rig = CockpitInteractiveRig.Create(root.transform, panel, helm);

                Assert.AreEqual(16, rig.Controls.Count, "All 16 physical 3D cockpit controls must register BoxCollider hitboxes");
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
                    Vector3 origin = worldCenter + new Vector3(0f, 0.25f, -0.75f);
                    var ray = new Ray(origin, (worldCenter - origin).normalized);
                    rig.StepMouseInteraction(ray, false, false, false, Vector2.zero, 0f);
                    Assert.AreEqual(kv.Key, rig.HoveredControl, "3D mouse raycast must resolve hitbox for " + kv.Key);
                }

                // Full preflight sequence using ONLY 3D mouse clicks on cockpit switches!
                Assert.IsFalse(panel.MainBus);
                Click3DControl(rig, CockpitControlId.MainBusSwitch);
                Assert.IsTrue(panel.MainBus);

                Click3DControl(rig, CockpitControlId.FuelFeedSwitch);
                Assert.IsTrue(panel.FuelFeed);

                Click3DControl(rig, CockpitControlId.HatchSwitch);
                Assert.IsTrue(panel.HatchSealed);

                Assert.IsFalse(rig.SafetyCoverOpen);
                Click3DControl(rig, CockpitControlId.MasterArmCover);
                Assert.IsTrue(rig.SafetyCoverOpen);

                Click3DControl(rig, CockpitControlId.MasterArmSwitch);
                Assert.IsTrue(panel.Arm);

                var ignite = Click3DControl(rig, CockpitControlId.IgniteButton);
                Assert.IsTrue(ignite.RequestedIgnite);

                // Scroll wheel & drag on 3D Throttle Lever + 3D Flight Stick + MFD Galaxy jump
                float beforeThrottle = helm.Throttle;
                Scroll3DControl(rig, CockpitControlId.ThrottleLever, 0.25f);
                Assert.Greater(helm.Throttle, beforeThrottle);

                var galSwitch = Click3DControl(rig, CockpitControlId.MfdToggleGalaxy);
                Assert.IsTrue(galSwitch.RequestedHyperjump);
                helm.Step(0.1f, 0f, 0f, 0f);
                for (int i = 0; i < 20; i++) helm.Step(0.1f, 0f, 0f, 0f);
                Assert.AreEqual(GalaxyId.Andromeda, helm.CurrentGalaxy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static CockpitActionResult Click3DControl(CockpitInteractiveRig rig, CockpitControlId id)
        {
            var hb = rig.Controls[id];
            var box = hb.GetComponent<BoxCollider>();
            Vector3 target = hb.transform.TransformPoint(box.center);
            Vector3 origin = target + new Vector3(0f, 0.2f, -0.6f);
            return rig.StepMouseInteraction(new Ray(origin, (target - origin).normalized), true, true, false, Vector2.zero, 0f);
        }

        static void Scroll3DControl(CockpitInteractiveRig rig, CockpitControlId id, float scroll)
        {
            var hb = rig.Controls[id];
            var box = hb.GetComponent<BoxCollider>();
            Vector3 target = hb.transform.TransformPoint(box.center);
            Vector3 origin = target + new Vector3(0f, 0.2f, -0.6f);
            rig.StepMouseInteraction(new Ray(origin, (target - origin).normalized), false, false, false, Vector2.zero, scroll);
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

            // Real manual 6-DOF piloting changes 3D trajectory & attitude continuously, plus hyperjumps across both galaxies
            var helm = new OpenSpaceHelm();
            helm.EnterOrbitAround(CelestialBodyId.Earth);
            SpaceVector p0 = helm.PositionKm;
            helm.SetManualControl(true);
            helm.SetThrottle(0.85f);
            helm.Boost = true;
            for (int i = 0; i < 15; i++) helm.Step(0.1f, 0.35f, -0.25f, 0.15f);
            Assert.Greater(SpaceVector.Distance(p0, helm.PositionKm), 1000.0);

            helm.StartHyperjump(CelestialBodyId.AndromedaPrime);
            for (int i = 0; i < 20; i++) helm.Step(0.1f, 0f, 0f, 0f);
            Assert.AreEqual(GalaxyId.Andromeda, helm.CurrentGalaxy);
            Assert.AreEqual(StarSystemId.AndromedaCore, helm.CurrentSystem);
            Assert.IsTrue(helm.InLandingRange, "Hyperjump places ship directly on final landing orbit");
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
            Assert.AreEqual(6, StoryChapters.Entries.Length);
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
            Assert.AreEqual(13, GameRules.Version);
            Assert.AreEqual(51, ExpandedContent.Blocks.Length);
            Assert.AreEqual(69, ExpandedContent.MaxBlock);
            Assert.AreEqual(88, SurfaceLayers.Names.Length);
            Assert.AreEqual(12, BiomeGenerator.Names.Length);
            Assert.AreEqual(20, WildlifeSimulation.SpeciesCount);
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

            // Diverse villagers across all 10 professions build clean 24-vertex meshes with zero child colliders
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
                    foreach (var mf in filters) Assert.AreEqual(24, mf.sharedMesh.vertexCount);
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
        }
    }
}
