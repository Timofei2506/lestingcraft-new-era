using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Gameplay;
using LestingCraft.Runtime;

static class GreedyAndSpaceFullCoreCheck
{
    static int checks;
    static void Check(bool ok, string msg)
    {
        if (!ok) throw new Exception("FAIL: " + msg);
        checks++;
    }

    static ChunkData GenerateRealTerrainChunk(int ox, int oz, Dimension dim, int seed)
    {
        var buf = new NativeArray<ushort>(ChunkData.BlockCount, Allocator.TempJob);
        var job = new GenerateChunkJob
        {
            OriginX = ox,
            OriginZ = oz,
            Seed = seed,
            Dimension = dim,
            ContentVersion = 7,
            Blocks = buf
        };
        job.Schedule(256, 16).Complete();
        var data = new ChunkData(buf.ToArray());
        buf.Dispose();
        return data;
    }

    static int Main()
    {
        // 1. Live benchmark of ChunkMesher non-greedy vs greedy across all 5 worlds (Earth, Moon, Mars, Venus, Mercury)!
        long rawTris = 0, greedyTris = 0;
        long rawVerts = 0, greedyVerts = 0;
        long rawColTris = 0, greedyColTris = 0;

        Dimension[] dims = { Dimension.Lestingia, Dimension.Moon, Dimension.Mars, Dimension.Venus, Dimension.Mercury };
        foreach (var dim in dims)
        {
            var c00 = GenerateRealTerrainChunk(0, 0, dim, 20261020);
            var nE = GenerateRealTerrainChunk(16, 0, dim, 20261020);
            var nW = GenerateRealTerrainChunk(-16, 0, dim, 20261020);
            var nN = GenerateRealTerrainChunk(0, 16, dim, 20261020);
            var nS = GenerateRealTerrainChunk(0, -16, dim, 20261020);
            Func<int, int, int, ushort> neighbor = (wx, wy, wz) =>
            {
                if (wy < 0 || wy >= 256) return 0;
                if (wx >= 16 && wx < 32 && wz >= 0 && wz < 16) return nE.GetBlock(wx - 16, wy, wz);
                if (wx >= -16 && wx < 0 && wz >= 0 && wz < 16) return nW.GetBlock(wx + 16, wy, wz);
                if (wx >= 0 && wx < 16 && wz >= 16 && wz < 32) return nN.GetBlock(wx, wy, wz - 16);
                if (wx >= 0 && wx < 16 && wz >= -16 && wz < 0) return nS.GetBlock(wx, wy, wz + 16);
                return 0;
            };

            var raw = ChunkMesher.Build(c00, 0, 0, neighbor, seed: 20261020, decorate: true, detailQuality: 3, greedy: false);
            var opt = ChunkMesher.Build(c00, 0, 0, neighbor, seed: 20261020, decorate: true, detailQuality: 3, greedy: true);

            Check(raw.Vertices.Count > 0 && opt.Vertices.Count > 0, $"non-empty meshes for {dim}");
            Check(opt.Vertices.Count < raw.Vertices.Count, $"greedy reduces vertices on {dim}: {opt.Vertices.Count} < {raw.Vertices.Count}");
            Check(opt.OpaqueTriangles.Count < raw.OpaqueTriangles.Count, $"greedy reduces opaque triangles on {dim}: {opt.OpaqueTriangles.Count} < {raw.OpaqueTriangles.Count}");
            Check(opt.Vertices.Count == opt.Normals.Count && opt.Vertices.Count == opt.Tangents.Count && opt.Vertices.Count == opt.UVs.Count && opt.Vertices.Count == opt.Colors.Count, $"attribute parity on {dim}");
            foreach (var list in new[] { opt.OpaqueTriangles, opt.WaterTriangles, opt.TransparentTriangles, opt.DetailTriangles })
                for (int i = 0; i < list.Count; i++)
                    Check(list[i] >= 0 && list[i] < opt.Vertices.Count, $"valid vertex index on {dim}");

            rawVerts += raw.Vertices.Count;
            greedyVerts += opt.Vertices.Count;
            rawTris += raw.OpaqueTriangles.Count / 3;
            greedyTris += opt.OpaqueTriangles.Count / 3;
            rawColTris += raw.ColliderTriangles.Count / 3;
            greedyColTris += opt.ColliderTriangles.Count / 3;
        }
        double reductionPct = 100.0 * (1.0 - (double)greedyTris / rawTris);
        double colReductionPct = 100.0 * (1.0 - (double)greedyColTris / rawColTris);
        Check(reductionPct > 50.0, $"overall greedy triangle reduction > 50% (got {reductionPct:F1}%)");
        Check(colReductionPct > 50.0, $"overall greedy collider reduction > 50% (got {colReductionPct:F1}%)");

        // 2. Space targets, wire keys & all 3 Planetary Boss encounters (Mars, Venus, Mercury)
        Check(SpaceFlightVisuals.TargetWireKey(SpaceTargetKind.Moon) == "moon", "moon wire key");
        Check(SpaceFlightVisuals.TargetWireKey(SpaceTargetKind.Mars) == "mars", "mars wire key");
        Check(SpaceFlightVisuals.TargetWireKey(SpaceTargetKind.Venus) == "venus", "venus wire key");
        Check(SpaceFlightVisuals.TargetWireKey(SpaceTargetKind.Mercury) == "mercury", "mercury wire key");
        Check(SpaceFlightVisuals.TargetWireKey(SpaceTargetKind.Earth) == "earth", "earth wire key");
        Check(SpaceFlightVisuals.TargetWireKey(SpaceTargetKind.OrbitalStation) == "station", "station wire key");

        var world = new GameObject("WorldTest").AddComponent<VoxelWorld>();
        foreach (var bDim in new[] { Dimension.Mars, Dimension.Venus, Dimension.Mercury })
        {
            var boss = PlanetaryBossController.SpawnForPlanet(null, bDim, world, 2026, false, new Vector3(0, 40, 0));
            Check(boss != null && boss.MaxHealth >= 260 && !string.IsNullOrEmpty(boss.BossName), $"boss spawned for {bDim}");
            var aim = new Ray(boss.transform.position + new Vector3(0, 4.2f, -6f), Vector3.forward);
            bool hit = boss.TryHitBoss(aim, 20f, boss.MaxHealth + 25, out bool defeatedNow, out ItemStack[] drops);
            Check(hit && defeatedNow && boss.Defeated && drops != null && drops.Length > 0, $"boss hit & drops loot on {bDim}");
        }

        // 3. SurvivalSession HasFullSpaceSuit() gating: requires all 4 sealed armor slots [4..7] + CompressedAir/LiquidOxygen
        var go = new GameObject("SessionHost");
        var player = new GameObject("PlayerHost").AddComponent<FirstPersonController>();
        player.Camera = new GameObject("CamHost").AddComponent<Camera>();
        var game = new GameObject("GameHost").AddComponent<PrototypeGame>();
        var session = go.AddComponent<SurvivalSession>();
        session.Initialize(game, world, player, (int)GameMode.Survival);
        Check(!session.HasFullSpaceSuit(), "no suit at start");

        session.Inventory.equipment[4] = new ItemStack { item = 105, count = 1, durability = 100 };
        session.Inventory.equipment[5] = new ItemStack { item = 106, count = 1, durability = 100 };
        session.Inventory.equipment[6] = new ItemStack { item = 107, count = 1, durability = 100 };
        session.Inventory.equipment[7] = new ItemStack { item = 108, count = 1, durability = 100 };
        Check(!session.HasFullSpaceSuit(), "armor without air canister still not pressurized");

        session.Inventory.Add(FuelChain.CompressedAir, 4);
        Check(session.HasFullSpaceSuit(), "sealed armor + CompressedAir enables deep EVA");

        // 4. Multi-planet VoxelWorld edit persistence (Earth + Moon + Mars)
        var seedSaves = new[]
        {
            new SavedBlock { realm = (int)Dimension.Lestingia, x = 2, y = 50, z = 2, block = 8 },
            new SavedBlock { realm = (int)Dimension.Moon, x = 4, y = 45, z = 4, block = 9 }
        };
        world.SwitchPlanet(Dimension.Mars, seedSaves);
        var merged = world.ExportAllMergedEdits(seedSaves);
        Check(merged.Length == 2, "multi-realm edits preserved across planet switches");

        // 5. Exact EditMode parity checks: AtmosphereTests.Meadow (<10000 detail tris), VisualSettings defaults, WildlifeSimulation legacy(3) + expanded(12), and all 12 AnimalVisual rigs!
        var meadow = new ChunkData();
        for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++) meadow.SetBlock(x, 20, z, BlockRegistry.GrassId);
        var meadowMesh = ChunkMesher.Build(meadow, -16, 32, (x, y, z) => 0, 2506, true);
        Check(meadowMesh.DetailTriangles.Count > 0 && meadowMesh.DetailTriangles.Count < 10000, $"Meadow detail triangles within budget: {meadowMesh.DetailTriangles.Count}");

        var gMed = new ChunkMeshData();
        var gUltra = new ChunkMeshData();
        for (int x = 0; x < 8; x++)
        {
            LestingCraft.Presentation.SurfaceDetailMesher.AddGrass(gMed, x, 10, 0, 0, 0, 2506, 0);
            LestingCraft.Presentation.SurfaceDetailMesher.AddGrass(gUltra, x, 10, 0, 0, 0, 2506, 1);
        }
        Check(gMed.Vertices.Count > 100 && gUltra.Vertices.Count > gMed.Vertices.Count * 2, $"Ultra grass vertices {gUltra.Vertices.Count} > 2 * medium {gMed.Vertices.Count} (>100)");

        var padRoot = new GameObject("PadNaNTest");
        var padVis = LaunchSiteVisual.Create(padRoot.transform, new Vector3Int(168, 70, 168), 2506);
        var validSnap = new LestingCraft.Networking.NetSpaceMission { active = true, from = "earth", to = "moon", seconds = 1800, elapsed = 720, position = new[] { 149797870700d, 0d, 2000000d }, crewCount = 2 };
        Check(padVis.SyncMoonFlight(validSnap), "valid moon snapshot syncs");
        var beforeNaN = padVis.ShuttleRoot.position;
        validSnap.position[0] = double.NaN;
        Check(!padVis.SyncMoonFlight(validSnap) && padVis.ShuttleRoot.position == beforeNaN, "NaN snapshot rejected without moving hull");

        var found12 = new System.Collections.Generic.HashSet<int>();
        foreach (int surf in new[] { 3, 6, 14, 27, 33 })
            for (int ring = -4; ring <= 4; ring++)
            {
                var fullSim = new WildlifeSimulation(20261020 + ring * 17, null, 7) { SurfaceAt = (x, z) => surf };
                fullSim.Spawn(
                    new AnimalObserver { id = "p", x = ring * 128f, y = 52f, z = surf * 96f, alive = true },
                    (x, z, y, r, h) => 50f);
                foreach (var a in fullSim.Animals) found12.Add(a.species);
            }
        Check(found12.Count == 12, $"All 12 wildlife species discovered across biome substrates (got {found12.Count})");

        var vs = new LestingCraft.Presentation.VisualSettings();
        Check(vs.ShadowResolution == 2048 && Math.Abs(vs.RenderScale - 1f) < 1e-4f && vs.PostEffects, "VisualSettings default contract");
        vs.RenderScale = .2f; vs.ViewDistance = 999; vs.Clamp();
        Check(Math.Abs(vs.RenderScale - .85f) < 1e-4f && vs.ViewDistance == 5, "VisualSettings clamp contract");
        vs.Ultra();
        Check(vs.Preset == 1 && vs.ShadowResolution == 4096 && vs.VolumetricQuality == 2, "VisualSettings Ultra preset contract");

        var legacySim = new WildlifeSimulation(2506);
        legacySim.Spawn(new AnimalObserver { id = "p", x = 0, y = 51.62f, z = 0, alive = true }, (x, z, y, r, h) => 50f);
        var legacySpecies = new System.Collections.Generic.HashSet<int>();
        foreach (var a in legacySim.Animals) legacySpecies.Add(a.species);
        Check(legacySpecies.Count == 3, $"legacy WildlifeSimulation(seed) preserves exact 3-species contract (got {legacySpecies.Count})");

        for (int sp = 0; sp < 12; sp++)
        {
            var ao = new GameObject("AnimalRig_" + sp);
            var av = ao.AddComponent<LestingCraft.Presentation.AnimalVisual>();
            av.Build(sp);
            Check(av.Head != null && av.Legs.Count == (sp == 11 ? 2 : 4) && (sp != 11 || av.Wings.Count == 2), $"AnimalVisual rig valid for species {sp}");
        }

        Console.WriteLine($"PASS: {checks} greedy-mesh, full-space & 12-species runtime assertions! Real 5-world chunk triangles {rawTris} -> {greedyTris} (-{reductionPct:F1}%), collider {rawColTris} -> {greedyColTris} (-{colReductionPct:F1}%), vertices {rawVerts} -> {greedyVerts}, meadow detail={meadowMesh.DetailTriangles.Count}!");
        return 0;
    }
}
