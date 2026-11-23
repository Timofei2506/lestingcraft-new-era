using NUnit.Framework;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.Runtime;

namespace LestingCraft.Tests
{
    public sealed class GameFeelAndScaleTests
    {
        [Test]
        public void PlayerVisualStandsTwoBlocksTallAndFitsTwoBlockEyeLevel()
        {
            var root = new GameObject("TwoBlockPlayer");
            try
            {
                var vis = root.AddComponent<CharacterVisual>();
                vis.Build();
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                Assert.GreaterOrEqual(renderers.Length, 10);
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                Assert.AreEqual(0f, combined.min.y, 0.04f, "Boot soles should rest on Y = 0");
                Assert.AreEqual(2.00f, combined.max.y, 0.06f, "Character crown should stand ~2.00 blocks tall");
                Assert.AreEqual(1.78f, PlayerCapsule.EyeHeight, 0.01f);
                Assert.Less(PlayerCapsule.Height + PlayerCapsule.Skin * 2f, 2.00f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AllThirtyFourMobSculptsHaveRealisticBlockProportions()
        {
            Assert.AreEqual(34, AnimalSculpts.SpeciesNames.Length);
            Assert.AreEqual(34, AnimalSculpts.SpeciesScale.Length);

            // Small critters strictly below 1 block tall
            foreach (int small in new[] { 1, 5, 8, 9, 10, 11, 12, 13, 15, 16, 18, 25, 31, 32 })
            {
                var d = AnimalSculpts.For(small);
                Assert.Less(d.Height, 0.90f, "Small species should be under 1 block tall: " + small);
            }

            // Large beasts, towering mutants, and cosmic whale
            Assert.Greater(AnimalSculpts.For(4).Height, 1.55f, "Bear height");
            Assert.Greater(AnimalSculpts.For(6).Height, 1.95f, "Bison height");
            Assert.Greater(AnimalSculpts.For(14).Height, 2.35f, "Camel taller than 2-block player");
            Assert.Greater(AnimalSculpts.For(17).Height, 2.55f, "Moose antlers above player");
            Assert.Greater(AnimalSculpts.For(22).Depth, 2.20f, "Rad-Scorpion length");
            Assert.Greater(AnimalSculpts.For(27).Height, 2.50f, "Bone Stalker towers over player");
            Assert.Greater(AnimalSculpts.For(28).Height, 2.95f, "Trinitite Golem > 3 blocks");

            var whale = AnimalSculpts.For(33);
            Assert.Greater(whale.Height, 2.30f, "Sky Whale height");
            Assert.Greater(whale.Width, 5.00f, "Sky Whale wingspan");
            Assert.Greater(whale.Depth, 6.00f, "Sky Whale length");
        }

        [Test]
        public void ProceduralAudioSynthesizesFiniteNonSilentClipsAndCleansUp()
        {
            var host = new GameObject("AudioTestHost");
            try
            {
                var audio = GameAudio.Ensure(host.transform);
                string[] keys =
                {
                    "step_grass", "step_stone", "step_wood", "step_snow", "step_water",
                    "mine_stone", "break_stone", "place_block", "swing", "hit_flesh",
                    "bow_shot", "craft_done", "ui_click", "amb_wind", "amb_radia"
                };
                foreach (string key in keys)
                {
                    var clip = audio.GetClip(key);
                    Assert.IsNotNull(clip, "Missing synthesized clip: " + key);
                    Assert.Greater(clip.samples, 32, "Clip too short: " + key);
                    var samples = new float[clip.samples];
                    clip.GetData(samples, 0);
                    float energy = 0f;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        Assert.IsFalse(float.IsNaN(samples[i]) || float.IsInfinity(samples[i]), "Invalid sample in " + key);
                        energy += Mathf.Abs(samples[i]);
                    }
                    Assert.Greater(energy, 0.1f, "Silent clip: " + key);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AnimalHitFlashRestoresZeroAlphaOnReset()
        {
            var go = new GameObject("WolfFlashTest");
            try
            {
                var vis = go.AddComponent<AnimalVisual>();
                vis.Build(3);
                vis.TriggerHit(Vector3.back);
                vis.ResetMotion();
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var cols = mf.sharedMesh.colors;
                    for (int i = 0; i < cols.Length; i++)
                        Assert.AreEqual(0f, cols[i].a, 0.001f);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CrouchLedgeGuardPreventsWalkingOffSingleBlockPlatform()
        {
            // Single solid stone block at (0, 10, 0); air everywhere else
            System.Func<int, int, int, ushort> world = (x, y, z) => (x == 0 && y == 10 && z == 0) ? (ushort)1 : (ushort)0;

            // Trying to step way off +X into empty air from (0.85, 11.02, 0.50) gets clamped to 0 on X, while Z inside block moves freely
            Vector3 clamped = FirstPersonController.ClampLedgeStep(new Vector3(0.85f, 11.02f, 0.50f), new Vector3(0.45f, 0f, 0.10f), 0.24f, world);
            Assert.AreEqual(0f, clamped.x, 1e-4f, "X motion over abyss must be clamped while crouching");
            Assert.AreEqual(0.10f, clamped.z, 1e-4f, "Z motion parallel to ledge should slide smoothly");
        }

        [Test]
        public void UpgradedThreeDimensionalWorldObjectsMeshWithoutOpaqueCubeLeak()
        {
            ushort[] sculptedBlocks = { 15, 16, 17, 54, 55, 56, 61, 72, 73, 74, 75, 76, 77, 85, 86, 87, 89 };
            foreach (ushort id in sculptedBlocks)
            {
                Assert.IsTrue(LestingCraft.World.BlockRegistry.IsCustomPlacedModel(id), "Should use 3D model: " + id);
                Assert.IsFalse(LestingCraft.World.BlockRegistry.Occludes(id), "3D object should not occlude neighbors: " + id);
                var mesh = VoxelItemModels.MeshFor(id);
                Assert.IsNotNull(mesh, "Missing 3D mesh for block " + id);
                Assert.Greater(mesh.vertexCount, 120, "Sparse 3D mesh for block " + id);

                var c = new LestingCraft.World.ChunkData();
                c.SetBlock(4, 4, 4, id);
                var built = LestingCraft.World.ChunkMesher.Build(c, 0, 0, (x, y, z) => 0);
                Assert.AreEqual(0, built.OpaqueTriangles.Count, "Custom 3D block must not emit opaque 1x1x1 cube faces: " + id);
            }
            Assert.AreEqual(0.24f, LestingCraft.World.BlockRegistry.ColliderHeight(LestingCraft.World.BlockRegistry.SteelRailId), 0.001f);
        }

        [Test]
        public void WorldVisualEffectsBuildsSlashAndSparksOutsideCameraHierarchy()
        {
            var host = new GameObject("VfxHost");
            try
            {
                var vfx = WorldVisualEffects.Ensure(host.transform);
                Assert.AreEqual(4, vfx.ActiveParticleSystemCount);
                vfx.StartSlash(new Vector3(0f, 1.8f, 0f), Quaternion.identity, 172);
                vfx.BurstSparks(new Vector3(1f, 2f, 3f), Vector3.up, Color.yellow, 10);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void InfiniteAstrophysicalUniverseScalesHabitableZoneAndHyperjumpsAcrossLightYears()
        {
            // Verify star-flux physics across distant sectors (including >100,000 ly away!)
            for (long sx = 12000; sx <= 12015; sx++)
            {
                var sys = ProceduralUniverse.System(sx, -4500, 890);
                Assert.Greater(sys.MassSolar, 0.01);
                Assert.Greater(sys.LuminositySolar, 0.0);
                var planets = ProceduralUniverse.Planets(sys);
                double starRadiusAu = sys.StarRadiusKm / 149597870.7;
                foreach (var p in planets)
                {
                    Assert.Greater(p.OrbitAu, starRadiusAu * 3.0, "Planet must never orbit inside stellar photosphere");
                    var moons = ProceduralUniverse.Moons(p);
                    Assert.AreEqual(Mathf.Min(6, p.MoonCount), moons.Length);
                    for (int m = 0; m < moons.Length; m++)
                        Assert.Greater(moons[m].OrbitRadiusKm, p.RadiusKm * 3.0, "Moon must orbit outside Roche limit");
                }
            }

            // Verify OpenSpaceHelm infinite procedural scanning & hyperjump into landing orbit!
            var helm = new OpenSpaceHelm { Active = true, PositionKm = new SpaceVector(8.5e18, -3.2e18, 1.1e18) };
            Assert.IsTrue(helm.CycleProceduralWorld(1));
            Assert.IsTrue(helm.DeepSpaceProceduralMode);
            Assert.IsTrue(helm.StartProceduralHyperjump());
            for (int step = 0; step < 30 && helm.InHyperjump; step++)
                helm.Step(0.08f, 0f, 0f, 0f);
            Assert.IsFalse(helm.InHyperjump);
            Assert.IsTrue(helm.InLandingRange, "Hyperjump must place ship inside procedural exoplanet landing orbit");
        }

        [Test]
        public void AllWorldKingdomQuietfordAndPlanetaryStructuresAreRebuiltAndWalkable()
        {
            // 1. All 10 rebuilt world structures (ArchitecturePlan 0..9) have 3D vertical relief and walkable portals
            Assert.AreEqual(10, LestingCraft.World.ArchitecturePlan.Radius.Length);
            for (int kind = 0; kind < 10; kind++)
            {
                int r = LestingCraft.World.ArchitecturePlan.Radius[kind];
                int solidAboveGround = 0;
                int maxH = 0;
                for (int x = -r; x <= r; x++)
                    for (int z = -r; z <= r; z++)
                        for (int y = 1; y <= 26; y++)
                        {
                            ushort b = LestingCraft.World.ArchitecturePlan.Apply(kind, x, y, z);
                            if (b != 0)
                            {
                                solidAboveGround++;
                                if (y > maxH) maxH = y;
                            }
                        }
                Assert.Greater(solidAboveGround, 250, "Structure plan " + kind + " must have substantial 3D volume");
                Assert.GreaterOrEqual(maxH, 8, "Structure plan " + kind + " must have tall 3D silhouette");
            }

            // 2. Imperial Capital (KingdomPlan at 420, -340) samples local (dx, dz) accurately up to 45m spire
            int kBase = LestingCraft.World.KingdomPlan.BaseY(2506);
            Assert.IsTrue(LestingCraft.World.KingdomPlan.TrySample(LestingCraft.World.KingdomPlan.CenterX, kBase, LestingCraft.World.KingdomPlan.CenterZ, 2506, out ushort kFloor));
            Assert.AreNotEqual(0, kFloor);
            Assert.IsTrue(LestingCraft.World.KingdomPlan.TrySample(LestingCraft.World.KingdomPlan.CenterX, kBase + 24, LestingCraft.World.KingdomPlan.CenterZ + 9, 2506, out ushort kDrum));
            Assert.AreEqual(30, kDrum, "Golden drum of Imperial Cathedral-Palace");
            Assert.IsTrue(LestingCraft.World.KingdomPlan.TrySample(LestingCraft.World.KingdomPlan.CenterX, kBase + 45, LestingCraft.World.KingdomPlan.CenterZ - 1, 2506, out ushort kSpire));
            Assert.AreEqual(11, kSpire, "45m crystal finial atop central dome");

            int[] kx = { 0, 79, -70, 0, -24, 22, 52, -52, 74, -74, 96, -96, 16, -16 };
            int[] kz = { 38, -2, 54, -74, -18, -18, 0, 0, -40, -40, -20, -20, 72, 72 };
            for (int i = 0; i < kx.Length; i++)
            {
                int wx = LestingCraft.World.KingdomPlan.CenterX + kx[i];
                int wz = LestingCraft.World.KingdomPlan.CenterZ + kz[i];
                Assert.IsTrue(LestingCraft.World.KingdomPlan.TrySample(wx, kBase + 1, wz, 2506, out ushort c1));
                Assert.IsTrue(LestingCraft.World.KingdomPlan.TrySample(wx, kBase + 2, wz, 2506, out ushort c2));
                Assert.AreEqual(0, c1, "Kingdom citizen feet clear at index " + i);
                Assert.AreEqual(0, c2, "Kingdom citizen head clear at index " + i);
            }

            // 3. Rebuilt Chapter One / Quietford (contentVersion >= 11) + frozen <= 10 compatibility
            int sBase = LestingCraft.Story.StoryWorld.Floor(2506);
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(88, sBase + 5, 48, 2506, out ushort v8Lamp, 8));
            Assert.AreEqual(LestingCraft.World.BlockRegistry.LanternBlockId, v8Lamp, "v8 StoryWorld frozen");
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(96, sBase + 20, 69, 2506, out ushort v11Belfry, 11));
            Assert.AreEqual(46, v11Belfry, "Quietford v11 Town Hall copper clocktower rises above plaza");
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(48, sBase + 3, -87, 2506, out ushort v11Rune, 11));
            Assert.AreEqual(11, v11Rune, "Watermill rune crystal preserved at exact quest target");
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(176, sBase + 1, -112, 2506, out ushort v11Altar, 11));
            Assert.AreNotEqual(0, v11Altar, "Sanctuary boss altar dais intact");
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(176, sBase + 2, -112, 2506, out ushort v11AltarAir, 11));
            Assert.AreEqual(0, v11AltarAir, "Sanctuary boss arena clear above altar");
            // Bridge v11 river channel + open stone arch + slender 1x1 streetlamps (no T-hammer crossbars)
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(-3, sBase - 4, 9, 2506, out ushort v11River, 11));
            Assert.AreEqual(7, v11River, "Bridge v11 river water flows beneath stone arch");
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(-3, sBase - 1, 9, 2506, out ushort v11ArchAir, 11));
            Assert.AreEqual(0, v11ArchAir, "Bridge v11 vaulted arch span is open above water");
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(88, sBase + 4, 48, 2506, out ushort v11Bollard, 11));
            Assert.AreEqual(LestingCraft.World.BlockRegistry.LanternBlockId, v11Bollard, "Slender 1x1 Quietford v11 streetlamp bollard");
            Assert.IsTrue(LestingCraft.Story.StoryWorld.TrySample(87, sBase + 4, 48, 2506, out ushort v11NoHammer, 11));
            Assert.AreEqual(0, v11NoHammer, "No T-hammer crossbar on v11 streetlamps");

            // 4. New 3D Planetary & Alien Structures across Moon, Mars, Europa & AndromedaPrime
            foreach (var body in new[] { LestingCraft.World.PlanetBody.Moon, LestingCraft.World.PlanetBody.Mars, LestingCraft.World.PlanetBody.Europa, LestingCraft.World.PlanetBody.AndromedaPrime })
            {
                // Central landing / boss arena (|x| < 40, |z| < 40) stays 100% clear of planetary structures
                for (int tx = -36; tx <= 36; tx += 12)
                    for (int tz = -36; tz <= 36; tz += 12)
                        Assert.IsFalse(LestingCraft.World.PlanetTerrain.TrySamplePlanetaryStructure(tx, 50, tz, 2506, body, out _));

                bool foundStructure = false;
                for (int cx = -2; cx <= 2 && !foundStructure; cx++)
                    for (int cz = -2; cz <= 2 && !foundStructure; cz++)
                        for (int sx = cx * 128 + 36; sx < cx * 128 + 92 && !foundStructure; sx += 2)
                            for (int sz = cz * 128 + 36; sz < cz * 128 + 92 && !foundStructure; sz += 2)
                            {
                                if (Mathf.Abs(sx) < 56 && Mathf.Abs(sz) < 56) continue;
                                int h = LestingCraft.World.PlanetTerrain.Column(sx, sz, 2506, body).Height;
                                if (LestingCraft.World.PlanetTerrain.TrySamplePlanetaryStructure(sx, h + 6, sz, 2506, body, out ushort sb) && sb != 0)
                                    foundStructure = true;
                            }
                Assert.IsTrue(foundStructure, "Expected deterministic 3D planetary structure on " + body);
            }
        }
    }
}
